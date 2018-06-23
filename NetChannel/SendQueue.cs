using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace NetChannel
{
    public class QueueState
    {
        public const int First = 1;
        public const int Second = 2;
        public const int Stop = 3;
    }

    public class SendTrough
    {
        public ANetChannel Channel { get; set; }
        public Packet Packet { get; set; }

        public void Send()
        {
            Channel.Send(Packet);
        }
    }

    /// <summary>
    /// 一个双缓冲生产消费队列，用于合并发送包，该队列的设计能减少多线程的锁竞争开销。
    /// </summary>
    public class SendQueue : IDisposable
    {
        private ConcurrentQueue<SendTrough> firstQueue = new ConcurrentQueue<SendTrough>();
        private ConcurrentQueue<SendTrough> secondQueue = new ConcurrentQueue<SendTrough>();        
        private byte state = QueueState.First;
        private Thread thread;
        private AutoResetEvent resetEvent = new AutoResetEvent(false);

        public void Start()
        {
            if(thread == null)
            {
                thread = new Thread(Send);
                thread.IsBackground = true;
                thread.Start();
            }
        }

        private int writeCount;
        public void Enqueue(SendTrough trough)
        {
            var count = trough.Packet.Data == null ? 0 : trough.Packet.Data.Length;
            Thread.VolatileWrite(ref writeCount, writeCount + count);
            if (writeCount > short.MaxValue)
            {
                resetEvent.WaitOne();
                Thread.VolatileWrite(ref writeCount, 0);
            }
            switch (Thread.VolatileRead(ref state))
            {
                case QueueState.First:
                    firstQueue.Enqueue(trough);
                    break;
                case QueueState.Second:
                    secondQueue.Enqueue(trough);
                    break;
            }
        }

        private async void Send()
        {
            var sendList = new HashSet<ANetChannel>();
            try
            {
                while (state != QueueState.Stop)
                {
                    while (true)
                    {
                        SendTrough trough;
                        if (state == QueueState.First)
                        {
                            if (secondQueue.IsEmpty)
                            {
                                if (!firstQueue.IsEmpty)
                                {
                                    Swap();
                                }
                                break;
                            }
                            else
                            {
                                if (secondQueue.TryDequeue(out trough))
                                {
                                    trough.Send();
                                    sendList.Add(trough.Channel);
                                }
                            }
                        }
                        else
                        {
                            if (firstQueue.IsEmpty)
                            {
                                if (!secondQueue.IsEmpty)
                                {
                                    Swap();
                                }
                                break;
                            }
                            else
                            {
                                if (firstQueue.TryDequeue(out trough))
                                {
                                    trough.Send();
                                    sendList.Add(trough.Channel);
                                }
                            }
                        }
                    }
                    resetEvent.Set();
                    if(sendList.Count > 0)
                    {
                        foreach (var channel in sendList)
                        {
                            await channel.StartSend();
                        }
                        sendList.Clear();
                    }
                    Thread.Sleep(1);
                }
            }
            catch(Exception e)
            {
                Console.Write(e.ToString());
            }

        }

        private void Swap()
        {
            if (Thread.VolatileRead(ref state) == QueueState.First)
            {
                Thread.VolatileWrite(ref state, QueueState.Second);
            }
            else
            {
                Thread.VolatileWrite(ref state, QueueState.First);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Thread.VolatileWrite(ref state, QueueState.Stop);
                }

                disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
