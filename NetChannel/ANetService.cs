using Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;

namespace NetChannel
{
    /// <summary>
    /// 网络通讯服务抽象类
    /// </summary>
    public abstract class ANetService
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="session"></param>
        public ANetService(Session session)
        {
            Session = session;
        }

        /// <summary>
        /// 发送列表
        /// </summary>
        private readonly HashSet<ANetChannel> senders = new HashSet<ANetChannel>();

        /// <summary>
        /// 最后一次检测心跳的时间
        /// </summary>
        private uint LastCheckTime;

        /// <summary>
        /// 心跳超时时长
        /// </summary>
        public static uint HeartbeatTime = 1000 * 20;

        /// <summary>
        /// 接受连接请求Socket
        /// </summary>
        protected Socket acceptor;

        /// <summary>
        /// 网络服务类型
        /// </summary>
        protected NetServiceType serviceType;

        /// <summary>
        /// 客户端连接ANetChannel
        /// </summary>
        protected ANetChannel ClientChannel;

        /// <summary>
        /// 发送包队列
        /// </summary>
        protected readonly ConcurrentQueue<SendTask> sendQueue = new ConcurrentQueue<SendTask>();

        /// <summary>
        /// 连接通道池
        /// </summary>
        public readonly ConcurrentDictionary<long, ANetChannel> Channels = new ConcurrentDictionary<long, ANetChannel>();

        /// <summary>
        /// 连接通道池
        /// </summary>
        public readonly ConcurrentDictionary<long, IEnumerable<IMessageHandler>> Handlers = new ConcurrentDictionary<long, IEnumerable<IMessageHandler>>();

        /// <summary>
        /// 消息会话
        /// </summary>
        public Session Session { get;private set; }

        /// <summary>
        /// 监听并接受Socket连接
        /// </summary>
        /// <returns></returns>
        public abstract void Accept();

        /// <summary>
        /// 发送连接请求与创建连接
        /// </summary>
        /// <returns></returns>
        public abstract ANetChannel Connect();
        
        /// <summary>
        /// 更新发送接收队列
        /// </summary>
        public virtual void Update()
        {
            if(serviceType == NetServiceType.Client && ClientChannel != null)
            {                
                ClientChannel.StartConnecting();
            }
            HandleSend();
            HandleReceive();
        }

        /// <summary>
        /// 插入一个数据包到发送队列中
        /// </summary>
        /// <param name="sendTask"></param>
        public void Enqueue(SendTask sendTask)
        {
            this.sendQueue.Enqueue(sendTask);
        }

        /// <summary>
        /// 处理数据发送回调函数
        /// </summary>
        protected void HandleSend()
        {
            try
            {
                while (!this.sendQueue.IsEmpty)
                {
                    if(this.sendQueue.TryPeek(out SendTask send))
                    {
                        if (send.Channel.Connected)
                        {
                            this.sendQueue.TryDequeue(out SendTask sendTask);
                            sendTask.Channel.TimeNow = TimeUitls.Now();
                            sendTask.WriteToBuffer();
                            this.senders.Add(sendTask.Channel);
                        }
                    }
                }

                foreach (var channel in this.senders)
                {
                    channel.StartSend();
                }
                this.senders.Clear();

                this.CheckHeadbeat();
            }
            catch (Exception e)
            {
#if DEBUG
                LogRecord.Log(LogLevel.Warn, "HandleSend", e);
#endif
            }
        }

        /// <summary>
        /// 所有管道接收数据
        /// </summary>
        private void HandleReceive()
        {
            var channels = Channels.Values;
            foreach (var channel in channels)
            {
                channel.StartRecv();
            }
        }

        /// <summary>
        /// 心跳检测
        /// </summary>
        private void CheckHeadbeat()
        {
            var now = TimeUitls.Now();

            var lastCheckSpan = now - this.LastCheckTime;
            if (lastCheckSpan < HeartbeatTime)
            {
                return;
            }

            if (this.serviceType == NetServiceType.Client)
            {
                if (this.ClientChannel == null)
                {
                    return;
                }
                if (!this.ClientChannel.Connected)
                {
                    return;
                }
                var timeSpan = now - this.ClientChannel.LastSendTime;
                if (timeSpan > HeartbeatTime)
                {
                    this.Session.SendMessage(new Packet
                    {
                        IsHeartbeat = true
                    });
#if DEBUG
                    LogRecord.Log(LogLevel.Info, "CheckHeadbeat", $"发送心跳包到服务端:{this.ClientChannel.RemoteEndPoint}.");
#endif
                }
            }
            else if (this.serviceType == NetServiceType.Server)
            {
                var channels = this.Channels.Values;
                foreach (var channel in channels)
                {
                    var timeSpan = now - channel.LastRecvTime;
                    if (timeSpan > HeartbeatTime)
                    {
#if DEBUG
                        LogRecord.Log(LogLevel.Info, "CheckHeadbeat", $"客户端:{channel.RemoteEndPoint}连接超时，心跳检测断开，心跳时长{timeSpan}.");
#endif
                        channel.DisConnect();
                    }
                }
            }
            LastCheckTime = now;
        }

        /// <summary>
        /// 添加一个通讯管道到连接对象池中
        /// </summary>
        /// <param name="channel"></param>
        protected void AddChannel(ANetChannel channel)
        {
            Channels.TryAdd(channel.Id, channel);
        }

        /// <summary>
        /// 添加一个MessageHandler到消息处理类池中
        /// </summary>
        /// <param name="channel"></param>
        protected void AddHandler(ANetChannel channel)
        {
            if (!Handlers.ContainsKey(channel.Id))
            {
                var handlers = MessageHandlerFactory.CreateHandlers(channel, this);
                Handlers[channel.Id] = handlers;
            }
        }

        /// <summary>
        /// 处理连接断开(服务端)
        /// </summary>
        /// <param name="channel"></param>
        protected void HandleDisConnectOnServer(ANetChannel channel)
        {
            try
            {
                if (Channels.TryRemove(channel.Id, out ANetChannel value))
                {
#if DEBUG
                    LogRecord.Log(LogLevel.Info, "HandleDisConnectOnServer", $"客户端:{channel.RemoteEndPoint}连接断开.");
#endif
                }
            }
            catch (Exception e)
            {
#if DEBUG
                LogRecord.Log(LogLevel.Warn, "HandleDisConnectOnServer", e);
#endif
            }
        }

        /// <summary>
        /// 处理连接断开(客户端)
        /// </summary>
        /// <param name="channel"></param>
        protected void HandleDisConnectOnClient(ANetChannel channel)
        {
            try
            {
                if (Channels.TryRemove(channel.Id, out ANetChannel value))
                {
#if DEBUG
                    LogRecord.Log(LogLevel.Info, "HandleDisConnectOnClient", $"与服务端{channel.RemoteEndPoint}连接断开.");
#endif
                }
            }
            catch (Exception e)
            {
#if DEBUG
                LogRecord.Log(LogLevel.Warn, "HandleDisConnectOnClient", e);
#endif
            }
        }
    }
}