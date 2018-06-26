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

        public override async Task AcceptAsync()
        {
            udpClient = udpClient ?? new UdpClient(this.endPoint);
            while (true)
            {
                UdpReceiveResult recvResult;
                try
                {
                    recvResult = await this.udpClient.ReceiveAsync();
                }
                catch(Exception e)
                {
                    LogRecord.Log(LogLevel.Warn, "DoAccept", e);
                }
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
