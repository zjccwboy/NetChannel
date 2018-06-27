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
        /// </summary>socketClient
        public const byte FIN = 3;
    }

    public class KcpChannel : ANetChannel
    {
        public int ConnectSN;
        private UdpClient socketClient;
        public uint remoteConn;
        private readonly Kcp kcp;
        private ConcurrentQueue<Packet> sendQueut = new ConcurrentQueue<Packet>();

        private static int synPacketSize = PacketParser.HeadMinSize;
        private static int ackPacketSize = PacketParser.HeadMinSize + sizeof(int);
        private static int finPacketSize = PacketParser.HeadMinSize + sizeof(int);

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        /// <param name="udpClient">Ip/端口</param>
        /// <param name="netService">网络服务</param>
        public KcpChannel(IPEndPoint endPoint, UdpClient udpClient, ANetService netService) : base(netService)
        {
            this.DefaultEndPoint = endPoint;
            socketClient = udpClient;

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

        public override async Task<bool> ReConnecting()
        {
            DisConnect();
            return await StartConnecting();
        }

        /// <summary>
        /// 模拟TCP三次握手连接服务端
        /// </summary>
        /// <returns></returns>
        public override async Task<bool> StartConnecting()
        {
            try
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                socketClient.Client.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);

                //发送SYN包
                var synPacket = new Packet
                {
                    KcpProtocal = KcpNetProtocal.SYN,
                };
                Send(synPacket);

                //接收服务端ACK包与SN
                UdpReceiveResult receiveResult;
                receiveResult = await socketClient.ReceiveAsync();
                RecvParser.WriteBuffer(receiveResult.Buffer, 0, ackPacketSize);
                RecvParser.Buffer.UpdateWrite(ackPacketSize);
                var ackPacket = RecvParser.ReadBuffer();
                if (!ackPacket.IsSuccess)
                {
                    return false;
                }

                //完成三次握手
                if (ackPacket.KcpProtocal == KcpNetProtocal.ACK)
                {
                    //服务端无应答，连接失败
                    if (ackPacket.Data == null)
                    {
                        return false;
                    }

                    ConnectSN = ackPacket.KcpConnectSN;
                    if (ConnectSN == 0)
                    {
                        return false;
                    }

                    //发送ACK+SN包告诉服务端连接成功
                    var snAckPacket = new Packet
                    {
                        KcpProtocal = KcpNetProtocal.ACK,
                        KcpConnectSN = ConnectSN,
                        Data = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt32(ConnectSN))),
                    };
                    Send(snAckPacket);
                    Connected = true;
                    OnConnect?.Invoke(this);
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
            packet.IsKcpConnect = true;
            packet.KcpConnectSN = ConnectSN;
            var bytes = PacketParser.GetPacketBytes(packet);
            SendToKcp(bytes);
        }

        private void SendToKcp(List<byte[]> buffers)
        {
            foreach (var buffer in buffers)
            {
                kcp.Send(buffer);
            }
        }

        public override async void StartRecv()
        {
            while (true)
            {
                UdpReceiveResult recvResult;
                try
                {
                    recvResult = await this.socketClient.ReceiveAsync();
                }
                catch (Exception e)
                {
                    LogRecord.Log(LogLevel.Warn, "StartRecv", e);
                    continue;
                }
                RecvParser.WriteBuffer(recvResult.Buffer, 0, recvResult.Buffer.Length);
                RecvParser.Buffer.UpdateWrite(recvResult.Buffer.Length);
                while (true)
                {
                    var packet = RecvParser.ReadBuffer();
                    if (!packet.IsSuccess)
                    {
                        break;
                    }
                    LastRecvHeartbeat = DateTime.Now;

                    if (packet.KcpProtocal == KcpNetProtocal.SYN)
                    {
                        HandleSYN(packet);
                        break;
                    }
                    else if (packet.KcpProtocal == KcpNetProtocal.ACK)
                    {
                        HandleACK(packet);
                        break;
                    }
                    else if (packet.KcpProtocal == KcpNetProtocal.FIN)
                    {
                        HandleFIN(packet);
                        break;
                    }

                    if (!netService.Channels.ContainsKey(packet.KcpConnectSN))
                    {
                        HandleACK(packet);
                    }

                    if (!packet.IsHeartbeat)
                    {
                        if (packet.IsRpc)
                        {
                            if (RpcDictionarys.TryRemove(packet.RpcId, out Action<Packet> action))
                            {
                                //执行RPC请求回调
                                action(packet);
                            }
                            else
                            {
                                OnReceive?.Invoke(packet);
                            }
                        }
                        else
                        {
                            OnReceive?.Invoke(packet);
                        }
                    }
                }
            }
        }
                
        private void HandleSYN(Packet packet)
        {
            //应答客户端SYN连接请求
            packet.KcpConnectSN = KcpConnectSN.CreateSN();
            while (!netService.Channels.ContainsKey(packet.KcpConnectSN))
            {
                packet.KcpConnectSN = KcpConnectSN.CreateSN();
            }
            packet.KcpProtocal = KcpNetProtocal.ACK;
            var bytes = PacketParser.GetPacketBytes(packet);
            SendToKcp(bytes);
        }

        private void HandleACK(Packet packet)
        {
            var ipEndPoint = this.socketClient.Client.RemoteEndPoint as IPEndPoint;
            var channel = new KcpChannel(ipEndPoint, this.socketClient, this.netService);
            channel.RemoteEndPoint = this.socketClient.Client.RemoteEndPoint;
            channel.LocalEndPoint = this.socketClient.Client.LocalEndPoint;
            channel.ConnectSN = packet.KcpConnectSN;
            channel.Id = ConnectSN;//KCP直接使用ConnectSN做ChannelId
            OnConnect?.Invoke(channel);
        }

        private void HandleFIN(Packet packet)
        {
            if (Connected)
            {
                Connected = false;
                OnDisConnect?.Invoke(this);
            }
        }

        public override Task StartSend()
        {
            if (Connected)
            {
                while (!this.sendQueut.IsEmpty)
                {
                    Packet packet;
                    if(this.sendQueut.TryDequeue(out packet))
                    {
                        var bytes = PacketParser.GetPacketBytes(packet);
                        foreach(var d in bytes)
                        {
                            kcp.Send(d);
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }

        public override void WriteSendBuffer(Packet packet)
        {
            sendQueut.Enqueue(packet);
        }

        public override void DisConnect()
        {
            try
            {
                Connected = false;
                //发送FIN包
                var finPacket = new Packet
                {
                    KcpProtocal = KcpNetProtocal.FIN,
                };
                Send(finPacket);
                OnDisConnect?.Invoke(this);
            }
            catch { }
        }

        private void Output(byte[] bytes, int count)
        {
            socketClient.Send(bytes, count, this.DefaultEndPoint);
        }

    }
}
