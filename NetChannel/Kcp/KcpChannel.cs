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
        private Kcp kcp;
        private byte[] cacheBytes;
        private uint sendIntervalTime = TimeUitls.Now();

        /// <summary>
        /// 构造函数,Connect
        /// </summary>
        /// <param name="socket">Ip/端口</param>
        /// <param name="netService">网络服务</param>
        /// <param name="connectConv">网络连接Conv</param>
        public KcpChannel(Socket socket, IPEndPoint endPoint, ANetService netService, uint connectConv) : base(netService, connectConv)
        {
            this.RemoteEndPoint = endPoint;
            this.NetSocket = socket;
            RecvParser = new PacketParser();
            SendParser = new PacketParser();

            this.kcp = new Kcp(this.Id, this.Output);
            kcp.SetMtu(512);
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
                LogRecord.Log(LogLevel.Warn, "StartConnecting", e);
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
            this.LastRecvTime = TimeUitls.Now();
            this.kcp.Input(bytes, offset, lenght);
        }

        /// <summary>
        /// 该方法并没有用
        /// </summary>
        public override void StartRecv()
        {
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
                    try
                    {
                        var packet = RecvParser.ReadBuffer();
                        if (!packet.IsSuccess)
                        {
                            break;
                        }
                        if (!packet.IsHeartbeat)
                        {
                            //LogRecord.Log(LogLevel.Error, "StartRecv", $"收到远程电脑:{recvResult.RemoteEndPoint}");
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
                            LogRecord.Log(LogLevel.Warn, "HandleRecv", $"接收到客户端:{this.RemoteEndPoint}心跳包.");
                        }
                    }
                    catch(Exception e)
                    {
                        DisConnect();
                        LogRecord.Log(LogLevel.Warn, "StartRecv", e);
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
                    length = length > 488 ? 488 : length;
                    var count = kcp.Send(this.SendParser.Buffer.First, offset, length);
                    if (count > 0)
                    {
                        this.SendParser.Buffer.UpdateRead(count);
                        this.LastSendTime = TimeUitls.Now();
                    }
                }
            }
            SetKcpSendTime();
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
                ConnectSender.SendFIN(this.NetSocket, this.RemoteEndPoint, this);
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
            try
            {
                this.NetSocket.SendTo(bytes, 0, count, SocketFlags.None, this.RemoteEndPoint);
            }
            catch(Exception e)
            {
                DisConnect();
                LogRecord.Log(LogLevel.Warn, "Output", e);
            }
        }

        /// <summary>
        /// 设置KCP重传时间
        /// </summary>
        private void SetKcpSendTime()
        {
            kcp.Update(this.sendIntervalTime);
            sendIntervalTime = TimeUitls.Now();
            this.sendIntervalTime = this.kcp.Check(this.sendIntervalTime);
        }        

    }
}
