using Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetChannel
{
    public abstract class ANetService
    {
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
        public abstract Task AcceptAsync();
        /// <summary>
        /// 发送连接请求与创建连接
        /// </summary>
        /// <returns></returns>
        public abstract Task<ANetChannel> ConnectAsync();
        /// <summary>
        /// 发送队列
        /// </summary>
        internal abstract WorkQueue SendQueue { get;}

        protected void AddChannel(ANetChannel channel)
        {
            Channels.TryAdd(channel.Id, channel);
        }

        private volatile bool reConnectIsStart = false;
        protected async Task ReConnecting(ANetChannel channel)
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
            var isConnected = await channel.ReConnecting();
            if (isConnected)
            {
                reConnectIsStart = false;
                return;
            }
            await Task.Delay(3000).ContinueWith(async (t) =>
            {
                reConnectIsStart = false;
                if (!channel.Connected)
                {
                    await ReConnecting(channel);
                }
            });
        }

        protected void AddHandler(ANetChannel channel)
        {
            if (!Handlers.ContainsKey(channel.Id))
            {
                var handlers = MessageHandlerFactory.CreateHandlers(channel, this);
                Handlers[channel.Id] = handlers;
            }
        }

        protected void DoAccept(ANetChannel channel)
        {
            try
            {
                channel.OnDisConnect = DoDisConnectOnServer;
                channel.Connected = true;
                AddChannel(channel);
                AddHandler(channel);
                channel.StartRecv();
                LogRecord.Log(LogLevel.Info, "DoAccept", $"接受客户端:{channel.RemoteEndPoint}连接成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "DoAccept", e);
            }
        }

        protected void DoConnect(ANetChannel channel)
        {
            try
            {
                channel.OnDisConnect = DoDisConnectOnClient;
                channel.Connected = true;
                AddChannel(channel);
                AddHandler(channel);
                channel.StartRecv();
                LogRecord.Log(LogLevel.Info, "DoAccept", $"连接服务端:{channel.RemoteEndPoint}成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "DoConnect", e);
            }
        }

        protected void DoDisConnectOnServer(ANetChannel channel)
        {
            try
            {
                if (Channels.TryRemove(channel.Id, out ANetChannel valu))
                {
                    Handlers.TryRemove(channel.Id, out IEnumerable<IMessageHandler> handler);
                    LogRecord.Log(LogLevel.Info, "DoAccept", $"客户端:{channel.RemoteEndPoint}连接断开...");
                }
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "DoDisConnectOnServer", e);
            }
        }

        protected async void DoDisConnectOnClient(ANetChannel channel)
        {
            try
            {
                if (Channels.TryRemove(channel.Id, out ANetChannel valu))
                {
                    LogRecord.Log(LogLevel.Info, "DoAccept", $"与服务端{channel.RemoteEndPoint}连接断开...");
                    await ReConnecting(channel);
                }
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "DoConnect", e);
            }
        }
    }
}