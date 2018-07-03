using Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        /// 连接通道池
        /// </summary>
        public readonly ConcurrentDictionary<long, ANetChannel> Channels = new ConcurrentDictionary<long, ANetChannel>();

        /// <summary>
        /// 连接通道池
        /// </summary>
        protected readonly ConcurrentDictionary<long, IEnumerable<IMessageHandler>> Handlers = new ConcurrentDictionary<long, IEnumerable<IMessageHandler>>();

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
        /// 发送队列
        /// </summary>
        internal abstract WorkQueue SendQueue { get;}

        /// <summary>
        /// 添加一个通讯管道到连接对象池中
        /// </summary>
        /// <param name="channel"></param>
        protected void AddChannel(ANetChannel channel)
        {
            Channels.TryAdd(channel.Id, channel);
        }

        private volatile bool reConnectIsStart = false;
        /// <summary>
        /// 断线重连方法
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        protected void ReConnecting(ANetChannel channel)
        {
            if (reConnectIsStart)
            {
                return;
            }
            reConnectIsStart = true;
            if (channel.Connected)
            {
                reConnectIsStart = false;
                return;
            }
            LogRecord.Log(LogLevel.Info, "ReConnecting", "重新连接...");
            Task.Delay(3000).ContinueWith((t) =>
            {
                reConnectIsStart = false;
                if (!channel.Connected)
                {
                    ReConnecting(channel);
                }
            });
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
                if (Channels.TryRemove(channel.Id, out ANetChannel valu))
                {
                    Handlers.TryRemove(channel.Id, out IEnumerable<IMessageHandler> handler);
                    LogRecord.Log(LogLevel.Info, "HandleDisConnectOnServer", $"客户端:{channel.RemoteEndPoint}连接断开...");
                }
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleDisConnectOnServer", e.ConvertToJson());
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
                if (Channels.TryRemove(channel.Id, out ANetChannel valu))
                {
                    LogRecord.Log(LogLevel.Info, "HandleDisConnectOnClient", $"与服务端{channel.RemoteEndPoint}连接断开...");
                    ReConnecting(channel);
                }
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleDisConnectOnClient", e.ConvertToJson());
            }
        }
    }
}