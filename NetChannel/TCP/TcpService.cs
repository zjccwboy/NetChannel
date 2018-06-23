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
            tcpListener = new TcpListener(endPoint);
            tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            tcpListener.Server.NoDelay = true;
            tcpListener.Start();
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
            await channel.StartConnecting();
            channel.OnDisConnect = RemoveChannel;
            AddChannel(channel);
            AddHandler(channel);
            return channel;
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
