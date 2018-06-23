using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetChannel
{
    /// <summary>
    /// TCP服务
    /// </summary>
    public class TcpService : ANetService
    {
        private TcpListener tcpListener;
        private IPEndPoint endPoint;

        public TcpService(IPEndPoint endPoint, Session session) : base(session)
        {
            sendQueue = new WorkQueue(session);
            this.endPoint = endPoint;
        }

        private readonly WorkQueue sendQueue;
        internal override WorkQueue SendQueue
        {
            get
            {
                return sendQueue;
            }
        }

        public override async Task AcceptAsync()
        {
            if(tcpListener == null)
            {
                tcpListener = new TcpListener(endPoint);
                tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                tcpListener.Server.NoDelay = true;
                tcpListener.Start();
            }

            while (true)
            {
                try
                {
                    var client = await tcpListener.AcceptTcpClientAsync();
                    var channel = new TcpChannel(endPoint);
                    channel.Client = client;
                    channel.OnConnect = DoConnect;
                    channel.OnDisConnect = DoDisConnect;
                    channel.OnConnect?.Invoke(channel);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        public override async Task<ANetChannel> ConnectAsync()
        {
            var channel = new TcpChannel(endPoint);
            channel.OnConnect = DoConnect;
            channel.OnDisConnect = DoDisConnect;
            var isConnected = await channel.StartConnecting();
            if (!isConnected)
            {
                await ReConnecting(channel);
            }
            return channel;
        }

        private async Task ReConnecting(ANetChannel channel)
        {
            if (channel.Connected)
            {
                return;
            }

            var isConnected = await channel.ReConnecting();
            if (!isConnected)
            {
                await Task.Delay(3000).ContinueWith(async (t) =>
                {
                    if (!channel.Connected)
                    {
                        Console.WriteLine("重新连接...");
                        await ReConnecting(channel);
                    }
                });
            }
        }

        private void AddChannel(ANetChannel channel)
        {
            Channels.TryAdd(channel.Id, channel);
        }

        private void AddHandler(ANetChannel channel)
        {
            var handlers = MessageHandlerFactory.CreateHandlers(channel, this);
            Handlers[channel.Id] = handlers;
        }

        private void DoConnect(ANetChannel channel)
        {
            channel.Connected = true;
            AddChannel(channel);
            AddHandler(channel);
            channel.StartRecv();
            Console.WriteLine("连接成功...");
        }

        private void DoDisConnect(ANetChannel channel)
        {
            Handlers.TryRemove(channel.Id, out IEnumerable<IMessageHandler> handler);
            Channels.TryRemove(channel.Id, out ANetChannel valu);
            Console.WriteLine("连接断开...");
        }

    }
}
