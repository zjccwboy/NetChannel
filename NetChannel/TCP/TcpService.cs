using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Common;

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
                    var channel = new TcpChannel(endPoint, client);
                    channel.RemoteEndPoint = client.Client.RemoteEndPoint;
                    channel.LocalEndPoint = client.Client.LocalEndPoint;
                    channel.OnConnect = DoAccept;
                    channel.OnConnect?.Invoke(channel);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
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
                LogRecord.Log(LogLevel.Info, "DoAccept", $"接受客户端:{channel.RemoteEndPoint}连接成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "DoAccept", e);
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
     }
}
