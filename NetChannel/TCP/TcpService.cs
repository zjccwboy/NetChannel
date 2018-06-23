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
                    channel.OnDisConnect = RemoveChannel;
                    AddChannel(channel);
                    AddHandler(channel);
                    Console.WriteLine("Accept 成功");
                    channel.Connected = true;
                    channel.StartRecv();
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
            channel.OnConnect = (c) =>
            {
               channel.StartRecv();
               Console.WriteLine("Connect 成功");
            };
            var isConnected = await channel.StartConnecting();
            if (!isConnected)
            {
                await ReConnecting(channel);
            }
            channel.OnDisConnect = RemoveChannel;
            AddChannel(channel);
            AddHandler(channel);
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

        private void RemoveChannel(ANetChannel channel)
        {
            Handlers.TryRemove(channel.Id, out IEnumerable<IMessageHandler> handler);
            Channels.TryRemove(channel.Id, out ANetChannel valu);
        }

    }
}
