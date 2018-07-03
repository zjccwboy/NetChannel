using Common;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NetChannel
{
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
    internal class WorkQueue
    {
        private ConcurrentQueue<SendTask> sendQueue = new ConcurrentQueue<SendTask>();
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
            HandleSend();
        }

        /// <summary>
        /// 插入一个数据包到发送队列中
        /// </summary>
        /// <param name="sendTask"></param>
        public void Enqueue(SendTask sendTask)
        {
            sendQueue.Enqueue(sendTask);
        }

        /// <summary>
        /// 处理数据发送回调函数
        /// </summary>
        private void HandleSend()
        {
            try
            {

                while (!sendQueue.IsEmpty)
                {
                    if(sendQueue.TryDequeue(out SendTask send))
                    {
                        send.WriteToBuffer();
                    }
                }

                //发送出去
                session.StartSend();
                session.CheckHeadbeat();
            }
            catch(Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleSend", e);
            }
        }
    }
}
