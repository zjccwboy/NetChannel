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
        private UdpClient socketClient;
        private Kcp kcp;
        private readonly byte[] cacheBytes = new byte[1400];
        private ConcurrentQueue<Packet> sendQueut = new ConcurrentQueue<Packet>();

        //private static int synPacketSize = PacketParser.HeadMinSize;
        //private static int ackPacketSize = PacketParser.HeadMinSize + sizeof(int);
        //private static int finPacketSize = PacketParser.HeadMinSize + sizeof(int);


        /// <summary>
        /// 构造函数,Connect
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        /// <param name="udpClient">Ip/端口</param>
        /// <param name="netService">网络服务</param>
        public KcpChannel(IPEndPoint endPoint, UdpClient udpClient, ANetService netService) : base(netService)
        {
            this.DefaultEndPoint = endPoint;
            this.RemoteEndPoint = endPoint as IPEndPoint;
            RecvParser = new PacketParser();
        }

        /// <summary>
        /// 构造函数,Accept
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        /// <param name="udpClient">Ip/端口</param>
        /// <param name="netService">网络服务</param>
        /// <param name="connectConv">网络连接Conv</param>
        public KcpChannel(IPEndPoint endPoint, UdpClient udpClient, ANetService netService, uint connectConv) : base(netService, connectConv)
        {
            this.DefaultEndPoint = endPoint;
            this.LocalEndPoint = endPoint;
            socketClient = udpClient;
            RecvParser = new PacketParser();            
        }

        public void InitKcp()
        {
            this.kcp = new Kcp(this.Id, this.Output);
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
                ConnectSender.SendSYN(this.socketClient, this.RemoteEndPoint);
                return false;
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartConnecting", e);
                return false;
            }
        }

        public void HandleRecv(UdpReceiveResult recvResult)
        {
            this.kcp.Input(recvResult.Buffer);
            while (true)
            {
                int n = kcp.PeekSize();
                if (n == 0)
                {
                    break;
                }

                int count = this.kcp.Recv(cacheBytes);
                if (count <= 0)
                {
                    break;
                }

                RecvParser.WriteBuffer(cacheBytes, 0, count);
                while (true)
                {
                    var packet = RecvParser.ReadBuffer();
                    if (!packet.IsSuccess)
                    {
                        break;
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

        public override async void StartRecv()
        {
            while (true)
            {
                UdpReceiveResult recvResult;
                try
                {
                    recvResult = await this.socketClient.ReceiveAsync();
                    LastRecvHeartbeat = TimeUitls.Now();                    
                }
                catch (Exception e)
                {
                    LogRecord.Log(LogLevel.Warn, "StartRecv", e);
                    continue;
                }
                HandleRecv(recvResult);
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
                        var bytes = RecvParser.GetPacketBytes(packet);
                        SendToKcp(bytes);
                        KcpStartSend();
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
                OnDisConnect?.Invoke(this);
            }
            catch { }
        }

        private void Output(byte[] bytes, int count)
        {
            socketClient.Send(bytes, count, this.RemoteEndPoint);
        }

        private void Send(Packet packet)
        {
            var bytes = RecvParser.GetPacketBytes(packet);
            SendToKcp(bytes);
            KcpStartSend();
        }

        private void SendToKcp(List<byte[]> buffers)
        {
            foreach (var buffer in buffers)
            {
                if (buffer != null)
                {
                    kcp.Send(buffer);
                }
            }
        }

        private void KcpStartSend()
        {
            kcp.Update(this.LastSendHeartbeat);
            this.LastSendHeartbeat = this.kcp.Check(this.LastSendHeartbeat);
        }        

    }
}
