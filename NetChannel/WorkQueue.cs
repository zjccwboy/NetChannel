using Common;
using System;
using System.Collections.Concurrent;
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
        /// <summary>
        /// 通讯管道对象
        /// </summary>
        public ANetChannel Channel { get; set; }

        /// <summary>
        /// 发送数据包
        /// </summary>
        public Packet Packet { get; set; }

        /// <summary>
        /// 将数据包写道发送缓冲区中
        /// </summary>
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

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="session">通讯会话接口类对象</param>
        public WorkQueue(Session session)
        {
            this.session = session;
        }

        /// <summary>
        /// 开始启动发送线程
        /// </summary>
        public void Start()
        {
            if(thread == null)
            {
                thread = new Thread(HandleSend);
                thread.IsBackground = true;
                thread.Start();
            }
        }

        private volatile int writeCount;
        /// <summary>
        /// 插入一个数据包到发送队列中
        /// </summary>
        /// <param name="sendTask"></param>
        public void Enqueue(SendTask sendTask)
        {
            var size = sendTask.Packet.Data == null ? 0 : sendTask.Packet.Data.Length + PacketParser.HeadMaxSize;
            writeCount += size;
            if (writeCount >= short.MaxValue)
            {
                enqueueResetEvent.WaitOne(1);
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

        /// <summary>
        /// 处理数据发送回调函数
        /// </summary>
        private async void HandleSend()
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
                                    //如果无连接包丢弃
                                    if (!sendTask.Channel.Connected)
                                    {
                                        continue;
                                    }
                                    sendTask.WriteToBuffer();
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
                                    //如果无连接包丢弃
                                    if (!sendTask.Channel.Connected)
                                    {
                                        continue;
                                    }
                                    sendTask.WriteToBuffer();
                                }
                            }
                        }
                    }
                    //发送出去
                    await session.StartSend();
                    enqueueResetEvent.Set();
                    doSendResetEvent.WaitOne(1);
                    session.CheckHeadbeat();
                }
            }
            catch(Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleSend", e);
            }
        }

        /// <summary>
        /// 切换队列
        /// </summary>
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
