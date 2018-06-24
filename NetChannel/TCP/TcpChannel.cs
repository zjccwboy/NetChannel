using System;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Net;

namespace NetChannel
{
    /// <summary>
    /// TCP通道类
    /// </summary>
    public class TcpChannel : ANetChannel
    {
        /// <summary>
        /// TCP Socket Client
        /// </summary>
        public TcpClient Client;

        /// <summary>
        /// RPC字典
        /// </summary>
        private ConcurrentDictionary<int, Action<Packet>> rpcDictionarys = new ConcurrentDictionary<int, Action<Packet>>();

        /// <summary>
        /// 同步多线程发送队列
        /// </summary>
        private SemaphoreSlim sendSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        public TcpChannel(IPEndPoint endPoint) : base()
        {
            this.DefaultEndPoint = endPoint;
            RecvParser = new PacketParser();
            SendParser = new PacketParser();
        }

        /// <summary>
        /// 开始连接
        /// </summary>
        /// <returns></returns>
        public override async Task<bool> StartConnecting()
        {
            try
            {
                Client = Client ?? new TcpClient();
                Client.NoDelay = true;
                await Client.ConnectAsync(DefaultEndPoint.Address, DefaultEndPoint.Port);
                Connected = true;
                RemoteEndPoint = DefaultEndPoint;
                LocalEndPoint = Client.Client.LocalEndPoint;
                OnConnect?.Invoke(this);
                return Connected;
            }
            catch (Exception e)
            {
                Console.Write(e.ToString());
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
            Client = new TcpClient();
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
                return !((Client.Client.Poll(1000, SelectMode.SelectRead) && (Client.Client.Available == 0)));                
            }
            catch (Exception e)
            {
                Console.Write(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public override async Task SendAsync(Packet packet)
        {
            try
            {
                var netStream = Client.GetStream();
                await sendSemaphore.WaitAsync();
                SendParser.WriteBuffer(packet);
                if (!netStream.CanWrite)
                {
                    return;
                }
                LastSendHeartbeat = DateTime.Now;
                while (SendParser.Buffer.DataSize > 0)
                {
                    await netStream.WriteAsync(SendParser.Buffer.First, SendParser.Buffer.FirstOffset, SendParser.Buffer.FirstCount);
                    SendParser.Buffer.UpdateRead(SendParser.Buffer.FirstCount);
                }
            }
            catch (Exception e)
            {
                Console.Write(e.ToString());
                DoError();
            }
            finally
            {
                sendSemaphore.Release();
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
                if (!Client.Connected)
                {
                    return;
                }

                if (!Connected)
                {
                    return;
                }

                LastSendHeartbeat = DateTime.Now;
                var netStream = Client.GetStream();

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
                Console.Write(e.ToString());
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
            rpcDictionarys.TryAdd(packet.RpcId, recvAction);
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
                    var netStream = Client.GetStream();
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
                        LastRecvHeartbeat = DateTime.Now;
                        if (!packet.IsHeartbeat)
                        {
                            if (packet.IsRpc)
                            {
                                if (rpcDictionarys.TryRemove(packet.RpcId, out Action<Packet> action))
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
                Console.Write(e.ToString());
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
                var netStream = Client.GetStream();
                netStream.Close();
                netStream.Dispose();
            }
            catch { }

            try
            {
                Client.Close();
                Client.Dispose();
            }
            catch { }
        }
    }
}
