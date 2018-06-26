using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetChannel
{
    public static class KcpNetProtocal
    {
        public const byte SYN = 1;
        public const byte ACK = 2;
        public const byte FIN = 3;
    }

    public class KcpChannel : ANetChannel
    {
        private UdpClient udpClient;
        private Kcp kcp;
        public uint remoteConn;
        private readonly byte[] cacheBytes = new byte[1400];

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        public KcpChannel(IPEndPoint endPoint) : base()
        {
            this.DefaultEndPoint = endPoint;
            RecvParser = new PacketParser();
            SendParser = new PacketParser();

            this.kcp = new Kcp(this.remoteConn, this.Output);
            kcp.SetMtu(512);
            kcp.NoDelay(1, 10, 2, 1);  //fast
        }
        

        public override void AddRequest(Packet packet, Action<Packet> recvAction)
        {
            RpcDictionarys.TryAdd(packet.RpcId, recvAction);
        }

        public override bool CheckConnection()
        {
            return Connected;
        }

        public override Task<bool> ReConnecting()
        {
            throw new NotImplementedException();
        }

        public override Task SendAsync(Packet packet)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> StartConnecting()
        {
            udpClient = udpClient ?? new UdpClient();
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);

            var packet = new Packet
            {
                KcpProtocal = KcpNetProtocal.SYN,
            };

            SendParser.WriteBuffer(packet);

            this.udpClient.Send(cacheBytes, 8, DefaultEndPoint);

        }

        public override void StartRecv()
        {
            throw new NotImplementedException();
        }

        public override Task StartSend()
        {
            throw new NotImplementedException();
        }

        public override void WriteSendBuffer(Packet packet)
        {
            throw new NotImplementedException();
        }

        public override void DisConnect()
        {
            try
            {
                Connected = false;
                OnDisConnect?.Invoke(this);
            }
            catch { }

            try
            {
                udpClient.Close();
                udpClient.Dispose();
            }
            catch { }
        }

        private void Output(byte[] bytes, int count)
        {
            this.udpClient.Send(bytes, count, this.DefaultEndPoint);
        }

    }
}
