using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace NetChannel
{
    /// <summary>
    /// 队列状态
    /// </summary>
    internal class QueueState
    {
        public const int First = 1;
        public const int Second = 2;
        public const int Stop = 3;
    }

    /// <summary>
    /// 发送任务
    /// </summary>
    internal class SendTask
    {
        public ANetChannel Channel { get; set; }
        public Packet Packet { get; set; }

        public void WriteToBuffer()
        {
            Channel.WriteSendBuffer(Packet);
        }
    }

    /// <summary>
    /// 一个双缓冲生产消费队列，用于合并发送包
    /// </summary>
    internal class WorkQueue : IDisposable
    {
        private ConcurrentQueue<SendTask> firstQueue = new ConcurrentQueue<SendTask>();
        private ConcurrentQueue<SendTask> secondQueue = new ConcurrentQueue<SendTask>();        
        private volatile byte state = QueueState.First;
        private Thread thread;
        private AutoResetEvent doSendResetEvent = new AutoResetEvent(false);
        private AutoResetEvent enqueueResetEvent = new AutoResetEvent(false);
        private readonly Session session;

        public WorkQueue(Session session)
        {
            this.session = session;
        }

        public void Start()
        {
            if(thread == null)
            {
                thread = new Thread(DoSend);
                thread.IsBackground = true;
                thread.Start();
            }
        }

        private volatile int writeCount;
        public void Enqueue(SendTask sendTask)
        {
            var size = sendTask.Packet.Data == null ? 0 : sendTask.Packet.Data.Length + PacketParser.HeadMaxSize;
            writeCount += size;
            if (writeCount >= short.MaxValue)
            {
                enqueueResetEvent.WaitOne();
                writeCount = 0;
            }
            switch (state)
            {
                case QueueState.First:
                    firstQueue.Enqueue(sendTask);
                    break;
                case QueueState.Second:
                    secondQueue.Enqueue(sendTask);
                    break;
            }

            doSendResetEvent.Set();
        }

        private async void DoSend()
        {
            try
            {
                while (state != QueueState.Stop)
                {
                    while (true)
                    {
                        SendTask sendTask;
                        if (state == QueueState.First)
                        {
                            if (secondQueue.IsEmpty)
                            {
                                if (!firstQueue.IsEmpty)
                                {
                                    Swap();
                                    continue;
                                }
                                break;
                            }
                            else
                            {
                                if (secondQueue.TryDequeue(out sendTask))
                                {
                                    if (sendTask.Channel.Connected)
                                    {
                                        sendTask.WriteToBuffer();
                                    }
                                }
                            }
                        }
                        else if(state == QueueState.Second)
                        {
                            if (firstQueue.IsEmpty)
                            {
                                if (!secondQueue.IsEmpty)
                                {
                                    Swap();
                                    continue;
                                }
                                break;
                            }
                            else
                            {
                                if (firstQueue.TryDequeue(out sendTask))
                                {
                                    if (sendTask.Channel.Connected)
                                    {
                                        sendTask.WriteToBuffer();
                                    }
                                }
                            }
                        }
                    }
                    await session.StartSend();
                    enqueueResetEvent.Set();
                    doSendResetEvent.WaitOne(Session.HeartbeatTime);
                    session.CheckHeadbeat();
                }
            }
            catch(Exception e)
            {
                Console.Write(e.ToString());
            }

        }

        private void Swap()
        {
            if (state == QueueState.First)
            {
                state = QueueState.Second;
            }
            else
            {
                state = QueueState.First;
            }
        }

        private bool disposedValue = false; // 要检测冗余调用
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    doSendResetEvent.Set();
                    state = QueueState.Stop;
                }
                disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }
    }
}
