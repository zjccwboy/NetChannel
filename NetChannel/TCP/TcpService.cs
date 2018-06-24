using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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
                    channel.RemoteEndPoint = client.Client.RemoteEndPoint;
                    channel.LocalEndPoint = client.Client.LocalEndPoint;
                    channel.Client = client;
                    channel.OnConnect = DoAccept;
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
            var isConnected = await channel.StartConnecting();
            if (!isConnected)
            {
                await ReConnecting(channel);
            }
            return channel;
        }


        private volatile bool reConnectIsStart = false;
        private async Task ReConnecting(ANetChannel channel)
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
            Console.WriteLine("重新连接...");
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

        private void AddChannel(ANetChannel channel)
        {
            Channels.TryAdd(channel.Id, channel);
        }

        private void AddHandler(ANetChannel channel)
        {
            if (!Handlers.ContainsKey(channel.Id))
            {
                var handlers = MessageHandlerFactory.CreateHandlers(channel, this);
                Handlers[channel.Id] = handlers;
            }
        }

        private void DoAccept(ANetChannel channel)
        {
            try
            {
                channel.OnDisConnect = DoDisConnectOnServer;
                channel.Connected = true;
                AddChannel(channel);
                AddHandler(channel);
                channel.StartRecv();
                Console.WriteLine($"接受客户端:{channel.RemoteEndPoint}连接成功...");
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void DoConnect(ANetChannel channel)
        {
            try
            {
                channel.OnDisConnect = DoDisConnectOnClient;
                channel.Connected = true;
                AddChannel(channel);
                AddHandler(channel);
                channel.StartRecv();
                Console.WriteLine($"连接服务端:{channel.RemoteEndPoint}成功...");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void DoDisConnectOnServer(ANetChannel channel)
        {
            try
            {
                if(Channels.TryRemove(channel.Id, out ANetChannel valu))
                {
                    Handlers.TryRemove(channel.Id, out IEnumerable<IMessageHandler> handler);
                    Console.WriteLine($"客户端:{channel.RemoteEndPoint}连接断开...");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async void DoDisConnectOnClient(ANetChannel channel)
        {
            try
            {
                if(Channels.TryRemove(channel.Id, out ANetChannel valu))
                {
                    Console.WriteLine($"与服务端{channel.RemoteEndPoint}连接断开...");
                    await ReConnecting(channel);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

    }
}
