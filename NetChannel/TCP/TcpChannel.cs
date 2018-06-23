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
        /// Ip/端口
        /// </summary>
        private IPEndPoint endPoint;

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
        public TcpChannel(IPEndPoint endPoint):base()
        {
            this.endPoint = endPoint;            
            RecvParser = new PacketParser();
            SendParser = new PacketParser();
        }

        /// <summary>
        /// 开始连接
        /// </summary>
        /// <returns></returns>
        public override async Task<bool> StartConnecting()
        {
            if (Connected)
                return true;
            try
            {
                await Client.ConnectAsync(endPoint.Address, endPoint.Port);
                if (CallConnect())
                {
                    Connected = true;
                    return true;
                }
                else
                {
                    await Task.Delay(3000).ContinueWith(async (t) =>
                    {
                        await StartConnecting();
                    });
                }
            }
            catch(Exception e)
            {
                Console.Write(e.ToString());
            }
            return false;
        }

        /// <summary>
        /// 重连
        /// </summary>
        /// <returns></returns>
        public override async Task<bool> ReConnecting()
        {
            int retry = 0;
            while (true)
            {
                if (!CallConnect() && Connected)
                {
                    Client.Close();
                    Client = new TcpClient();
                    Connected = false;
                }
                var isSuccess = await StartConnecting();
                if (isSuccess)
                {
                    return true;
                }
                retry++;
                if(retry == 5)
                {
                    return false;
                }
            }
        }

        private bool CallConnect()
        {
            try
            {
                return !((Client.Client.Poll(1000, SelectMode.SelectRead) && (Client.Client.Available == 0)) || !Client.Client.Connected);
            }
            catch(Exception e)
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
            catch(Exception e)
            {
                Console.Write(e.ToString());
                OnError?.Invoke(this, SocketError.SocketError);
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

                LastSendHeartbeat = DateTime.Now;
                var netStream = Client.GetStream();

                if(netStream == null)
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
                OnError?.Invoke(this, SocketError.SocketError);
            }
        }

        /// <summary>
        /// 异步发送一个RPC请求，并且等待信号通知结果，提供请求取消对象
        /// </summary>
        /// <param name="packet">发送数据包</param>
        /// <param name="cancellationToken">提供取消的对象</param>
        /// <returns></returns>
        public override async Task<Packet> CallRequestAsync(Packet packet, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<Packet>();
            var registration = cancellationToken.Register(() =>
            {
                if (rpcDictionarys.TryRemove(packet.RpcId, out Action<Packet> action))
                {
                    tcs.TrySetResult(new Packet { IsSuccess = false});
                }
            });
            await RequestAsync(packet, (p) => tcs.SetResult(p));
            return await tcs.Task;
        }

        /// <summary>
        /// 异步发送一个RPC请求，并且等待信号通知结果
        /// </summary>
        /// <param name="packet">发送数据包</param>
        /// <returns></returns>
        public override async Task<Packet> CallRequestAsync(Packet packet)
        {
            var tcs = new TaskCompletionSource<Packet>();
            await RequestAsync(packet, (p) => tcs.SetResult(p));
            return await tcs.Task;
        }

        /// <summary>
        /// 异步发送一个RPC请求，不等待结果
        /// </summary>
        /// <param name="packet">发送数据包</param>
        /// <param name="recvAction">请求回调方法</param>
        /// <returns></returns>
        public override async Task RequestAsync(Packet packet, Action<Packet> recvAction)
        {
            packet.IsRpc = true;
            packet.RpcId = RpcId;
            //插入RPC请求处理回调方法
            if(rpcDictionarys.TryAdd(packet.RpcId, recvAction))
            {
                await SendAsync(packet);
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
                        OnError?.Invoke(this, SocketError.SocketError);
                    }
                    RecvParser.Buffer.UpdateWrite(count);
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
                        LastRecvHeartbeat = DateTime.Now;
                    }
                }
            }
            catch (Exception e)
            {
                Console.Write(e.ToString());
                OnError?.Invoke(this, SocketError.SocketError);
            }
        }

        public override void DisConnect()
        {
            try
            {
                Connected = false;
                OnClose?.Invoke(this);
                var netStream = Client.GetStream();
                netStream.Close();
                netStream.Dispose();
            }
            catch{}

            try
            {
                Client.Close();
                Client.Dispose();
            }
            catch { }
        }
    }
}
