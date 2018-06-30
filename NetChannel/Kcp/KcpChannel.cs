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
    /// <summary>
    /// KCP网络连接协议
    /// </summary>
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

    /// <summary>
    /// KCP通讯管道
    /// </summary>
    public class KcpChannel : ANetChannel
    {
        private UdpClient socketClient;
        private Kcp kcp;
        private readonly byte[] cacheBytes = new byte[1400];
        private ConcurrentQueue<Packet> sendQueut = new ConcurrentQueue<Packet>();

        /// <summary>
        /// 构造函数,Connect
        /// </summary>
        /// <param name="recvResult">Udp接收数据包对象</param>
        /// <param name="udpClient">Ip/端口</param>
        /// <param name="netService">网络服务</param>
        public KcpChannel(UdpReceiveResult recvResult, UdpClient udpClient, ANetService netService) : base(netService)
        {
            this.LocalEndPoint = udpClient.Client.LocalEndPoint as IPEndPoint;
            this.RemoteEndPoint = recvResult.RemoteEndPoint;
            RecvParser = new PacketParser();
            SendParser = new PacketParser();
        }

        /// <summary>
        /// 构造函数,Accept
        /// </summary>
        /// <param name="recvResult">Udp接收数据包对象</param>
        /// <param name="udpClient">Ip/端口</param>
        /// <param name="netService">网络服务</param>
        /// <param name="connectConv">网络连接Conv</param>
        public KcpChannel(UdpReceiveResult recvResult, UdpClient udpClient, ANetService netService, uint connectConv) : base(netService, connectConv)
        {
            this.LocalEndPoint = udpClient.Client.LocalEndPoint as IPEndPoint;
            this.RemoteEndPoint = recvResult.RemoteEndPoint;
            socketClient = udpClient;
            RecvParser = new PacketParser();
            SendParser = new PacketParser();
        }

        /// <summary>
        /// 实例化KCP对象
        /// </summary>
        public void InitKcp()
        {
            this.kcp = new Kcp(this.Id, this.Output);
            kcp.SetMtu(512);
            kcp.NoDelay(1, 10, 2, 1);  //fast
        }

        /// <summary>
        /// 添加一个发送数据包到发送缓冲区队列中
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="recvAction"></param>
        public override void AddPacket(Packet packet, Action<Packet> recvAction)
        {
            RpcDictionarys.TryAdd(packet.RpcId, recvAction);
        }

        /// <summary>
        /// 查看当前连接状态
        /// </summary>
        /// <returns></returns>
        public override bool CheckConnection()
        {
            return Connected;
        }

        /// <summary>
        /// 重新连接
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// 处理KCP接收数据
        /// </summary>
        /// <param name="recvResult"></param>
        public void HandleRecv(UdpReceiveResult recvResult)
        {
            this.LastRecvTime = TimeUitls.Now();
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
                    else
                    {
                        LogRecord.Log(LogLevel.Warn, "HandleRecv", $"接收到客户端:{recvResult.RemoteEndPoint}心跳包");
                    }
                }
            }
        }

        /// <summary>
        /// 该方法并没有用
        /// </summary>
        public override void StartRecv()
        {
            //while (true)
            //{
            //    UdpReceiveResult recvResult;
            //    try
            //    {
            //        recvResult = await this.socketClient.ReceiveAsync();
            //        LastRecvTime = TimeUitls.Now();
            //    }
            //    catch (Exception e)
            //    {
            //        LogRecord.Log(LogLevel.Warn, "StartRecv", e);
            //        continue;
            //    }
            //    HandleRecv(recvResult);
            //}
        }

        /// <summary>
        /// 开始发送KCP数据包
        /// </summary>
        /// <returns></returns>
        public override Task StartSend()
        {

            if (Connected)
            {
                //while (!this.sendQueut.IsEmpty)
                //{
                //    this.LastSendTime = TimeUitls.Now();
                //    Packet packet;
                //    if (this.sendQueut.TryDequeue(out packet))
                //    {
                //        this.SendParser.WriteBuffer(packet);
                //    }
                //}

                while (this.SendParser.Buffer.DataSize > 0)
                {
                    var offset = this.SendParser.Buffer.FirstOffset;
                    var length = this.SendParser.Buffer.FirstCount;
                    length = length > 488 ? 488 : length;
                    var count = kcp.Send(this.SendParser.Buffer.First, offset, length);
                    if (count > 0)
                    {
                        this.SendParser.Buffer.UpdateRead(count);
                        SetKcpSendTime();
                    }
                }
                SetKcpSendTime();
            }

            //if (Connected)
            //{
            //    while (!this.sendQueut.IsEmpty)
            //    {
            //        this.LastSendTime = TimeUitls.Now();
            //        Packet packet;
            //        if (this.sendQueut.TryDequeue(out packet))
            //        {
            //            var bytes = RecvParser.GetPacketBytes(packet);
            //            SendToKcp(bytes);
            //        }
            //    }
            //    SetKcpSendTime();
            //}
            return Task.CompletedTask;
        }

        /// <summary>
        /// 把数据包写道缓冲区队列中
        /// </summary>
        /// <param name="packet"></param>
        public override void WriteSendBuffer(Packet packet)
        {
            this.SendParser.WriteBuffer(packet);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public override void DisConnect()
        {
            try
            {
                Connected = false;
                OnDisConnect?.Invoke(this);
            }
            catch { }
        }

        /// <summary>
        /// KCP发送回调函数
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="count"></param>
        private void Output(byte[] bytes, int count)
        {
            //LogRecord.Log(LogLevel.Warn, "Output", $"发送数据到:{this.RemoteEndPoint}");
            socketClient.Send(bytes, count, this.RemoteEndPoint);
        }

        /// <summary>
        /// 发送到KCP缓冲区中
        /// </summary>
        /// <param name="buffers"></param>
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

        private uint sendIntervalTime = TimeUitls.Now();

        /// <summary>
        /// 设置KCP重传时间
        /// </summary>
        private void SetKcpSendTime()
        {
            sendIntervalTime = TimeUitls.Now();
            kcp.Update(this.sendIntervalTime);
            this.sendIntervalTime = this.kcp.Check(this.sendIntervalTime);
        }        

    }
}
