using Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetChannel
{
    public static class KcpNetProtocal
    {
        /// <summary>
        /// 连接请求
        /// </summary>
        public const byte SYN = 1;
        /// <summary>
        /// 连接请求应答
        /// </summary>
        public const byte ACK = 2;
        /// <summary>
        /// 断开连接请求
        /// </summary>
        public const byte FIN = 3;
    }

    public class KcpChannel : ANetChannel
    {
        public int ConnectSN;
        public UdpClient UdpClient;
        public uint remoteConn;
        private readonly Kcp kcp;
        private readonly byte[] cacheBytes = new byte[1400];
        private ConcurrentQueue<Packet> sendQueut = new ConcurrentQueue<Packet>();

        private static int synPacketSize = PacketParser.HeadMinSize;
        private static int ackPacketSize = PacketParser.HeadMinSize + sizeof(int);
        private static int finPacketSize = PacketParser.HeadMinSize + sizeof(int);        

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        public KcpChannel(IPEndPoint endPoint) : base()
        {
            this.DefaultEndPoint = endPoint;

            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            UdpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);

            RecvParser = new PacketParser();

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

        /// <summary>
        /// 模拟TCP三次握手连接服务端
        /// </summary>
        /// <returns></returns>
        public override async Task<bool> StartConnecting()
        {

            try
            {
                //发送SYN包
                var synPacket = new Packet
                {
                    KcpProtocal = KcpNetProtocal.SYN,
                };
                Send(synPacket);

                //接收服务端ACK包与SN
                UdpReceiveResult receiveResult;
                receiveResult = await this.UdpClient.ReceiveAsync();
                RecvParser.WriteBuffer(receiveResult.Buffer, 0, ackPacketSize);
                RecvParser.Buffer.UpdateWrite(ackPacketSize);
                var ackPacket = RecvParser.ReadBuffer();
                if (!ackPacket.IsSuccess)
                {
                    return false;
                }

                //完成三次握手
                if(ackPacket.KcpProtocal == KcpNetProtocal.ACK)
                {
                    //服务端无应答，连接失败
                    if(ackPacket.Data == null)
                    {
                        return false;
                    }

                    ConnectSN = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(ackPacket.Data, 0));
                    if(ConnectSN == 0)
                    {
                        return false;
                    }

                    //发送ACK+SN包告诉服务端连接成功
                    var snAckPacket = new Packet
                    {
                        KcpProtocal = KcpNetProtocal.ACK,
                        Data = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt32(ConnectSN))),
                    };
                    Send(snAckPacket);
                    Connected = true;
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartConnecting", e);
                return false;
            }
        }

        private void Send(Packet packet)
        {
            var bytes = GetPacketBytes(packet);
            SendToKcp(bytes);
        }

        private void SendToKcp(List<byte[]> buffers)
        {
            foreach(var buffer in buffers)
            {
                kcp.Send(buffer);
            }
        }

        public override void StartRecv()
        {
            try
            {
                while (true)
                {

                }
            }
            catch(Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartConnecting", e);
            }
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

            }
            catch { }
        }

        private void Output(byte[] bytes, int count)
        {
            this.UdpClient.Send(bytes, count, this.DefaultEndPoint);
        }

        public List<byte[]> GetPacketBytes(Packet packet)
        {
            var bytes = new List<byte[]>();
            var bodySize = 0;
            if (packet.Data != null)
            {
                bodySize = packet.Data.Length;
                if (packet.Data.Length > PacketParser.BodyMaxSize)
                {
                    throw new ArgumentOutOfRangeException();
                }
            }

            int headSize = packet.IsRpc ? PacketParser.HeadMaxSize : PacketParser.HeadMinSize;
            int packetSize = headSize + bodySize;
            var sizeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt16(packetSize)));
            bytes.Add(sizeBytes);

            var flagBytes = new byte[1];
            if (packet.IsRpc)
            {
                flagBytes[0] |= 1;
            }
            if (packet.IsHeartbeat)
            {
                flagBytes[0] |= 1 << 1;
            }
            if (packet.IsCompress)
            {
                flagBytes[0] |= 1 << 2;
            }
            if (packet.IsEncrypt)
            {
                flagBytes[0] |= 1 << 3;
            }
            if (packet.KcpProtocal > 0)
            {
                flagBytes[0] |= (byte)(packet.KcpProtocal << 5);
            }

            bytes.Add(flagBytes);
            if (packet.IsRpc)
            {
                var rpcBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt32(packet.RpcId)));
                bytes.Add(rpcBytes);
            }
            if (packet.Data != null)
            {
                bytes.Add(packet.Data);
            }
            return bytes;
        }

    }
}
