using Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetChannel
{
    public class KcpService : ANetService
    {
        private IPEndPoint endPoint;
        private UdpClient udpClient;
        private KcpChannel acceptChannel;

        public KcpService(IPEndPoint endPoint, Session session) : base(session)
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

        public override Task AcceptAsync()
        {
            if(this.udpClient != null)
            {
                return Task.CompletedTask;
            }

            this.udpClient = new UdpClient(this.endPoint);
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            this.udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
            this.acceptChannel = new KcpChannel(this.endPoint, this.udpClient);
            this.acceptChannel.OnConnect = DoAccept;
            this.acceptChannel.StartRecv();

            return Task.CompletedTask;
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
            udpClient = udpClient ?? new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            var channel = new KcpChannel(this.endPoint, udpClient);
            var isConnected = await channel.StartConnecting();
            if (!isConnected)
            {
                await channel.ReConnecting();
            }
            return channel;
        }
    }
}
