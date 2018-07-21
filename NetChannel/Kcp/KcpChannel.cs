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
        /// </summary>
        public const byte FIN = 3;
    }

    /// <summary>
    /// KCP通讯管道
    /// </summary>
    public class KcpChannel : ANetChannel
    {
        private Kcp kcp;
        private byte[] cacheBytes;
        private int singlePacketLimit = Kcp.IKCP_MTU_DEF - Kcp.IKCP_OVERHEAD;
        private uint lastCheckTime = TimeUitls.Now();

        /// <summary>
        /// 构造函数,Connect
        /// </summary>
        /// <param name="socket">Socket</param>
        /// <param name="netService">网络服务</param>
        /// <param name="connectConv">网络连接Conv</param>
        public KcpChannel(Socket socket, IPEndPoint endPoint, ANetService netService, uint connectConv) : base(netService, connectConv)
        {
            this.RemoteEndPoint = endPoint;
            this.NetSocket = socket;
            RecvParser = new PacketParser();
            SendParser = new PacketParser();
        }

        public void InitKcp()
        {
            kcp = new Kcp(this.Id, this);
            kcp.SetOutput(this.Output);
            kcp.NoDelay(1, 10, 2, 1);  //fast
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
        public override void ReConnecting()
        {
            StartConnecting();
        }

        /// <summary>
        /// 模拟TCP三次握手连接服务端
        /// </summary>
        /// <returns></returns>
        public override void StartConnecting()
        {
            try
            {
                var now = TimeUitls.Now();
                if (now - this.LastConnectTime < ANetChannel.ReConnectInterval)
                {
                    return;
                }

                this.LastConnectTime = now;

                if (Connected)
                {
                    return;
                }

                ConnectSender.SendSYN(this.NetSocket, this.RemoteEndPoint);
            }
            catch (Exception e)
            {
#if DEBUG
                LogRecord.Log(LogLevel.Warn, "StartConnecting", e);
#endif
            }
        }

        /// <summary>
        /// 处理KCP接收数据
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="lenght"></param>
        public override void HandleRecv(byte[] bytes, int offset, int lenght)
        {
            cacheBytes = bytes;
            this.LastRecvTime = this.TimeNow;
            this.kcp.Input(bytes, offset, lenght);
        }

        /// <summary>
        /// 该方法并没有用
        /// </summary>
        public override void StartRecv()
        {
            this.LastRecvTime = TimeUitls.Now();
            SetKcpSendTime();
            while (true)
            {
                int n = kcp.PeekSize();
                if (n == 0)
                {
                    //LogRecord.Log(LogLevel.Error, "StartRecv", $"解包失败:{this.RemoteEndPoint}");
                    return;
                }

                int count = this.kcp.Recv(cacheBytes, 0, cacheBytes.Length);
                if (count <= 0)
                {
                    return;
                }

                RecvParser.WriteBuffer(cacheBytes, 0, count);
                while (true)
                {
                    try
                    {
                        var packet = RecvParser.ReadBuffer();
                        if (!packet.IsSuccess)
                        {
                            break;
                        }

                        if (!packet.IsHeartbeat)
                        {
                            //LogRecord.Log(LogLevel.Error, "StartRecv", $"收到远程电脑:{this.RemoteEndPoint}");
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
#if DEBUG
                            LogRecord.Log(LogLevel.Warn, "HandleRecv", $"接收到客户端:{this.RemoteEndPoint}心跳包.");
#endif
                        }
                    }
                    catch (Exception e)
                    {
                        DisConnect();
#if DEBUG
                        LogRecord.Log(LogLevel.Warn, "StartRecv", e);
#endif
                        return;
                    }
                }
            }            
        }        

        /// <summary>
        /// 开始发送KCP数据包
        /// </summary>
        /// <returns></returns>
        public override void StartSend()
        {
            if (Connected)
            {
                while (this.SendParser.Buffer.DataSize > 0)
                {
                    var offset = this.SendParser.Buffer.FirstReadOffset;
                    var length = this.SendParser.Buffer.FirstDataSize;
                    length = length > singlePacketLimit ? singlePacketLimit : length;
                    kcp.Send(this.SendParser.Buffer.First, offset, length);
                    this.SendParser.Buffer.UpdateRead(length);
                    if(length >= singlePacketLimit)
                    {
                        SetKcpSendTime();
                    }
                }
                SetKcpSendTime();
            }
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
                ConnectSender.SendFIN(this.NetSocket, this.RemoteEndPoint, this.Id);
                OnDisConnect?.Invoke(this);
            }
            catch { }
        }

        /// <summary>
        /// KCP发送回调函数
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="count"></param>
        /// <param name="user"></param>
        private void Output(byte[] bytes, int count, object user)
        {
            try
            {
                this.NetSocket.SendTo(bytes, 0, count, SocketFlags.None, this.RemoteEndPoint);
                this.LastSendTime = TimeUitls.Now();
            }
            catch(Exception e)
            {
                DisConnect();
#if DEBUG
                LogRecord.Log(LogLevel.Warn, "Output", e);
#endif
            }
        }

        /// <summary>
        /// 设置KCP重传时间
        /// </summary>
        private void SetKcpSendTime()
        {    
            if (this.TimeNow >= this.lastCheckTime)
            {
                kcp.Update(this.TimeNow);
                this.lastCheckTime = this.kcp.Check(this.TimeNow);
            }
        }
    }
}
