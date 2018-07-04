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
        /// 发送状态机
        /// </summary>
        private bool isSending;

        /// <summary>
        /// 接收状态机
        /// </summary>
        private bool isReceiving;

        /// <summary>
        /// 构造函数,Connect
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        /// <param name="netService">通讯网络服务对象</param>
        public TcpChannel(IPEndPoint endPoint, ANetService netService) : this(netService)
        {
            this.RemoteEndPoint = endPoint;
        }

        /// <summary>
        /// 构造函数,Accept
        /// </summary>
        /// <param name="endPoint">IP/端口</param>
        /// <param name="socket">TCP socket类</param>
        /// <param name="netService">通讯网络服务对象</param>
        public TcpChannel(IPEndPoint endPoint, Socket socket, ANetService netService) : this(netService)
        {
            this.LocalEndPoint = endPoint;
            this.NetSocket = socket;
        }

        /// <summary>
        /// 私有构造函数
        /// </summary>
        /// <param name="netService"></param>
        private TcpChannel(ANetService netService) : base(netService)
        {
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
                var now = TimeUitls.Now();
                if(now - this.LastConnectTime < ANetChannel.ReConnectInterval)
                {
                    return;
                }

                this.LastConnectTime = now;

                if (Connected)
                {
                    return;
                }

                if(this.NetSocket == null)
                {
                    this.NetSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    this.NetSocket.NoDelay = true;
                }

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
        public override void ReConnecting()
        {
            if (Connected)
            {
                DisConnect();
            }
            StartConnecting();
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
            if (isSending)
            {
                return;
            }

            SendData();
        }

        private void SendData()
        {
            try
            {
                isSending = true;

                if (!Connected)
                {
                    isSending = false;
                    return;
                }

                if (SendParser.Buffer.DataSize <= 0)
                {
                    isSending = false;
                    return;
                }

                this.outArgs.SetBuffer(SendParser.Buffer.First, SendParser.Buffer.FirstReadOffset, SendParser.Buffer.FirstDataSize);
                if (this.NetSocket.SendAsync(this.outArgs))
                {
                    return;
                }
                OnSendComplete(this.outArgs);
            }
            catch (Exception e)
            {
                isSending = false;
                LogRecord.Log(LogLevel.Warn, "StartSend", e.ConvertToJson());
                HandleError();
            }
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        public override void StartRecv()
        {
            try
            {
                if (isReceiving)
                {
                    return;
                }
                isReceiving = true;

                if (!Connected)
                {
                    isReceiving = false;
                    return;
                }

                this.inArgs.SetBuffer(RecvParser.Buffer.Last, RecvParser.Buffer.LastWriteOffset, RecvParser.Buffer.LastCapacity);
                if (this.NetSocket.ReceiveAsync(this.inArgs))
                {
                    return;
                }
                OnRecvComplete(this.inArgs);
            }
            catch (Exception e)
            {
                isReceiving = false;
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
                if (NetSocket == null)
                {
                    return;
                }
                OnDisConnect?.Invoke(this);
            }
            catch { }

            try
            {
                SendParser.Clear();
                RecvParser.Clear();
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

            this.LastConnectTime = TimeUitls.Now();
            e.RemoteEndPoint = null;
            this.Connected = true;
            OnConnect?.Invoke(this);

            this.StartRecv();
            this.StartSend();
        }

        private void OnDisconnectComplete(object o)
        {
            SocketAsyncEventArgs e = (SocketAsyncEventArgs)o;
            this.OnDisConnect?.Invoke(this);
        }

        private void OnRecvComplete(object o)
        {
            isReceiving = false;
            if (this.NetSocket == null)
            {
                return;
            }

            this.LastRecvTime = TimeUitls.Now();
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
            isSending = false;

            if (this.NetSocket == null)
            {
                return;
            }

            this.LastSendTime = TimeUitls.Now();
            SocketAsyncEventArgs e = (SocketAsyncEventArgs)o;

            if (e.SocketError != SocketError.Success)
            {
                this.OnError(this, e.SocketError);
                return;
            }

            this.SendParser.Buffer.UpdateRead(e.BytesTransferred);
            if(this.SendParser.Buffer.DataSize <= 0)
            {
                return;
            }

            this.SendData();
        }
    }
}
