using System;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Net;
using Common;

namespace NetChannel
{
    /// <summary>
    /// TCP通道类
    /// </summary>
    public class TcpChannel : ANetChannel
    {
        /// <summary>
        /// TCP Socket socketClient
        /// </summary>
        private TcpClient socketClient;

        /// <summary>
        /// 发送包解析器
        /// </summary>
        private PacketParser SendParser;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        /// <param name="netService">网络服务</param>
        public TcpChannel(IPEndPoint endPoint, ANetService netService) : base(netService)
        {
            this.DefaultEndPoint = endPoint;
            RecvParser = new PacketParser();
            SendParser = new PacketParser();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        /// <param name="tcpClient">Ip/端口</param>
        /// <param name="netService">网络服务</param>
        public TcpChannel(IPEndPoint endPoint, TcpClient tcpClient, ANetService netService) : base(netService)
        {
            this.DefaultEndPoint = endPoint;
            RecvParser = new PacketParser();
            SendParser = new PacketParser();
            socketClient = tcpClient;
        }

        /// <summary>
        /// 开始连接
        /// </summary>
        /// <returns></returns>
        public override async Task<bool> StartConnecting()
        {
            try
            {
                socketClient = socketClient ?? new TcpClient();
                socketClient.NoDelay = true;
                await socketClient.ConnectAsync(DefaultEndPoint.Address, DefaultEndPoint.Port);
                Connected = true;
                RemoteEndPoint = DefaultEndPoint;
                LocalEndPoint = socketClient.Client.LocalEndPoint;
                OnConnect?.Invoke(this);
                return Connected;
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartConnecting", e);
                return false;
            }
        }

        /// <summary>
        /// 重连
        /// </summary>
        /// <returns></returns>
        public override async Task<bool> ReConnecting()
        {
            DisConnect();
            socketClient = new TcpClient();
            Connected = false;
            return await StartConnecting();
        }

        /// <summary>
        /// 检查连接状态
        /// </summary>
        /// <returns></returns>
        public override bool CheckConnection()
        {
            try
            {
                return !((socketClient.Client.Poll(1000, SelectMode.SelectRead) && (socketClient.Client.Available == 0)));                
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "CheckConnection", e);
                return false;
            }
        }

        /// <summary>
        /// 写入发送包到缓冲区队列(合并发送)
        /// </summary>
        /// <param name="packet"></param>
        public override void WriteSendBuffer(Packet packet)
        {
            SendParser.WriteBuffer(packet);
        }

        /// <summary>
        /// 发送缓冲区队列中的数据(合并发送)
        /// </summary>
        /// <returns></returns>
        public override async Task StartSend()
        {
            try
            {
                if (!socketClient.Connected)
                {
                    return;
                }

                if (!Connected)
                {
                    return;
                }

                LastSendHeartbeat = TimeUitls.Now();
                var netStream = socketClient.GetStream();

                if (netStream == null)
                {
                    return;
                }

                while (SendParser.Buffer.DataSize > 0)
                {
                    if (!netStream.CanWrite)
                    {
                        return;
                    }
                    await netStream.WriteAsync(SendParser.Buffer.First, SendParser.Buffer.FirstOffset, SendParser.Buffer.FirstCount);
                    SendParser.Buffer.UpdateRead(SendParser.Buffer.FirstCount);
                }
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartSend", e);
                DoError();
            }
        }

        /// <summary>
        /// 插入RPC
        /// </summary>
        /// <param name="packet">发送数据包</param>
        /// <param name="recvAction">请求回调方法</param>
        /// <returns></returns>
        public override void AddRequest(Packet packet, Action<Packet> recvAction)
        {
            RpcDictionarys.TryAdd(packet.RpcId, recvAction);
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        public override async void StartRecv()
        {
            try
            {
                while (true)
                {
                    var netStream = socketClient.GetStream();
                    if (netStream == null)
                    {
                        return;
                    }

                    if (!netStream.CanRead)
                    {
                        return;
                    }

                    var count = await netStream.ReadAsync(RecvParser.Buffer.Last, RecvParser.Buffer.LastOffset, RecvParser.Buffer.LastCount);
                    if (count <= 0)
                    {
                        DoError();
                        return;
                    }
                    RecvParser.Buffer.UpdateWrite(count);
                    while (true)
                    {
                        var packet = RecvParser.ReadBuffer();
                        if (!packet.IsSuccess)
                        {
                            break;
                        }
                        LastRecvHeartbeat = TimeUitls.Now();
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
                            //Console.WriteLine($"接收到客户端:{RemoteEndPoint}心跳包...");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartRecv", e);
                DoError();
            }
        }

        private void DoError()
        {
            DisConnect();
            OnError?.Invoke(this);
        }

        public override void DisConnect()
        {
            try
            {
                Connected = false;
                OnDisConnect?.Invoke(this);
                var netStream = socketClient.GetStream();
                netStream.Close();
                netStream.Dispose();
            }
            catch { }

            try
            {
                socketClient.Close();
                socketClient.Dispose();
            }
            catch { }
        }
    }
}
