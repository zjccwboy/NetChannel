using System;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using Common;
using System.Threading;

namespace NetChannel
{
    /// <summary>
    /// TCP通讯管道
    /// </summary>
    public class TcpChannel : ANetChannel
    {
        private SocketAsyncEventArgs inArgs = new SocketAsyncEventArgs();
        private SocketAsyncEventArgs outArgs = new SocketAsyncEventArgs();

        /// <summary>
        /// 构造函数,Connect
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        /// <param name="netService">通讯网络服务对象</param>
        public TcpChannel(IPEndPoint endPoint, ANetService netService) : base(netService)
        {
            this.RemoteEndPoint = endPoint;
            this.inArgs.Completed += OnComplete;
            this.outArgs.Completed += OnComplete;
        }

        /// <summary>
        /// 构造函数,Accept
        /// </summary>
        /// <param name="endPoint">IP/端口</param>
        /// <param name="socket">TCP socket类</param>
        /// <param name="netService">通讯网络服务对象</param>
        public TcpChannel(IPEndPoint endPoint, Socket socket, ANetService netService) : base(netService)
        {
            this.LocalEndPoint = endPoint;
            this.NetSocket = socket;
            this.inArgs.Completed += OnComplete;
            this.outArgs.Completed += OnComplete;
        }

        /// <summary>
        /// 开始连接
        /// </summary>
        /// <returns></returns>
        public override void StartConnecting()
        {
            try
            {
                this.outArgs.RemoteEndPoint = this.RemoteEndPoint;
                if (this.NetSocket.ConnectAsync(this.outArgs))
                {
                    return;
                }
                OnConnectComplete(this.outArgs);
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartConnecting", e.ConvertToJson());
            }
        }

        /// <summary>
        /// 重连
        /// </summary>
        /// <returns></returns>
        public override bool ReConnecting()
        {
            DisConnect();
            Connected = false;
            StartConnecting();
            return Connected;
        }

        /// <summary>
        /// 检查连接状态
        /// </summary>
        /// <returns></returns>
        public override bool CheckConnection()
        {
            try
            {
                return !((NetSocket.Poll(1000, SelectMode.SelectRead) && (NetSocket.Available == 0)));                
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "CheckConnection", e.ConvertToJson());
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
        public override void StartSend()
        {
            try
            {
                while(SendParser.Buffer.DataSize > 0)
                {
                    this.outArgs.SetBuffer(RecvParser.Buffer.First, RecvParser.Buffer.FirstReadOffset, RecvParser.Buffer.FirstDataSize);
                    if (this.NetSocket.SendAsync(this.outArgs))
                    {
                        return;
                    }
                    OnSendComplete(this.outArgs);
                }
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartSend", e.ConvertToJson());
                HandleError();
            }
        }

        /// <summary>
        /// 添加一个发送数据包到发送缓冲区队列中
        /// </summary>
        /// <param name="packet">发送数据包</param>
        /// <param name="recvAction">请求回调方法</param>
        /// <returns></returns>
        public override void AddPacket(Packet packet, Action<Packet> recvAction)
        {
            RpcDictionarys.TryAdd(packet.RpcId, recvAction);
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        public override void StartRecv()
        {
            try
            {
                this.inArgs.SetBuffer(RecvParser.Buffer.Last, RecvParser.Buffer.LastWriteOffset, RecvParser.Buffer.LastCapacity);
                if (this.NetSocket.ReceiveAsync(this.inArgs))
                {
                    return;
                }
                OnRecvComplete(this.inArgs);
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartRecv", e.ConvertToJson());
                HandleError();
            }
        }

        /// <summary>
        /// 处理错误
        /// </summary>
        private void HandleError()
        {
            DisConnect();
            OnError?.Invoke(this, SocketError.SocketError);
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

            try
            {
                NetSocket.Close();
                NetSocket.Dispose();
                NetSocket = null;
            }
            catch { }
        }

        private void OnComplete(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    OneThreadSynchronizationContext.Instance.Post(this.OnConnectComplete, e);
                    break;
                case SocketAsyncOperation.Receive:
                    OneThreadSynchronizationContext.Instance.Post(this.OnRecvComplete, e);
                    break;
                case SocketAsyncOperation.Send:
                    OneThreadSynchronizationContext.Instance.Post(this.OnSendComplete, e);
                    break;
                case SocketAsyncOperation.Disconnect:
                    OneThreadSynchronizationContext.Instance.Post(this.OnDisconnectComplete, e);
                    break;
                default:
                    throw new Exception($"socket error: {e.LastOperation}");
            }
        }

        private void OnConnectComplete(object o)
        {
            if (this.NetSocket == null)
            {
                return;
            }

            SocketAsyncEventArgs e = (SocketAsyncEventArgs)o;
            if (e.SocketError != SocketError.Success)
            {
                this.HandleError();
                return;
            }

            e.RemoteEndPoint = null;
            this.Connected = true;

            this.StartRecv();
            this.StartSend();
        }

        private void OnDisconnectComplete(object o)
        {
            SocketAsyncEventArgs e = (SocketAsyncEventArgs)o;
            this.HandleError();
        }

        private void OnRecvComplete(object o)
        {
            if (this.NetSocket == null)
            {
                return;
            }

            SocketAsyncEventArgs e = (SocketAsyncEventArgs)o;

            if (e.SocketError != SocketError.Success)
            {
                this.HandleError();
                return;
            }

            if (e.BytesTransferred == 0)
            {
                this.HandleError();
                return;
            }
            RecvParser.Buffer.UpdateWrite(e.BytesTransferred);

            while (true)
            {
                var packet = RecvParser.ReadBuffer();
                if (!packet.IsSuccess)
                {
                    break;
                }
                LastRecvTime = TimeUitls.Now();
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
            this.StartRecv();
        }

        private void OnSendComplete(object o)
        {
            if (this.NetSocket == null)
            {
                return;
            }
            SocketAsyncEventArgs e = (SocketAsyncEventArgs)o;

            if (e.SocketError != SocketError.Success)
            {
                this.OnError(this, e.SocketError);
                return;
            }

            this.SendParser.Buffer.UpdateRead(e.BytesTransferred);

            this.StartSend();
        }
    }
}
