using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Common;

namespace NetChannel
{
    /// <summary>
    /// TCP通讯服务
    /// </summary>
    public class TcpService : ANetService
    {
        private IPEndPoint endPoint;
        private Socket acceptor;
        private readonly SocketAsyncEventArgs innArgs = new SocketAsyncEventArgs();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        /// <param name="session"></param>
        public TcpService(IPEndPoint endPoint, Session session) : base(session)
        {
            SendQueue = new WorkQueue(session);
            this.endPoint = endPoint;
        }


        /// <summary>
        /// 开始监听并接受连接请求
        /// </summary>
        /// <returns></returns>
        public override void Accept()
        {
            if(acceptor == null)
            {
                this.acceptor = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                this.acceptor.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                this.innArgs.Completed += this.OnAcceptComplete;

                this.acceptor.Bind(this.endPoint);
                this.acceptor.Listen(1000);
            }

            this.innArgs.AcceptSocket = null;
            if (this.acceptor.AcceptAsync(this.innArgs))
            {
                return;
            }
            OnAcceptComplete(this, this.innArgs);
        }

        private void OnAcceptComplete(object sender, SocketAsyncEventArgs o)
        {
            if (this.acceptor == null)
            {
                return;
            }
            SocketAsyncEventArgs e = o;

            if (e.SocketError != SocketError.Success)
            {
                LogRecord.Log(LogLevel.Warn, "OnAcceptComplete", $"accept error {e.SocketError}");
                return;
            }
            var channel = new TcpChannel(this.endPoint, e.AcceptSocket, this);
            HandleAccept(channel);

            this.Accept();
        }

        /// <summary>
        /// 发送连接请求
        /// </summary>
        /// <returns></returns>
        public override ANetChannel Connect()
        {
            var channel = new TcpChannel(endPoint, this);
            channel.OnConnect = HandleConnect;
            channel.StartConnecting();
            return channel;
        }

        /// <summary>
        /// 处理接受连接成功回调
        /// </summary>
        /// <param name="channel"></param>
        private void HandleAccept(ANetChannel channel)
        {
            try
            {
                channel.OnDisConnect = HandleDisConnectOnServer;
                channel.Connected = true;
                AddChannel(channel);
                AddHandler(channel);
                LogRecord.Log(LogLevel.Info, "HandleAccept", $"接受客户端:{channel.RemoteEndPoint}连接成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleAccept", e.ConvertToJson());
            }
        }

        /// <summary>
        /// 处理连接成功回调
        /// </summary>
        /// <param name="channel"></param>
        private void HandleConnect(ANetChannel channel)
        {
            try
            {
                channel.OnDisConnect = HandleDisConnectOnClient;
                channel.Connected = true;
                AddChannel(channel);
                AddHandler(channel);
                LogRecord.Log(LogLevel.Info, "HandleConnect", $"连接服务端:{channel.RemoteEndPoint}成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleConnect", e.ConvertToJson());
            }
        }
     }
}
