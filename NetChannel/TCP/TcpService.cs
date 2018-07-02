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
        private TcpListener tcpListener;
        private IPEndPoint endPoint;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint">Ip/端口</param>
        /// <param name="session"></param>
        public TcpService(IPEndPoint endPoint, Session session) : base(session)
        {
            sendQueue = new WorkQueue(session);
            this.endPoint = endPoint;
        }

        private readonly WorkQueue sendQueue;
        /// <summary>
        /// 合并数据包发送队列
        /// </summary>
        internal override WorkQueue SendQueue
        {
            get
            {
                return sendQueue;
            }
        }

        /// <summary>
        /// 开始监听并接受连接请求
        /// </summary>
        /// <returns></returns>
        public override async Task AcceptAsync()
        {
            if(tcpListener == null)
            {
                tcpListener = new TcpListener(endPoint);
                tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                tcpListener.Server.NoDelay = true;
                tcpListener.Start();
            }

            while (true)
            {
                try
                {
                    var client = await tcpListener.AcceptTcpClientAsync();
                    var channel = new TcpChannel(endPoint, client, this);
                    channel.RemoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                    channel.OnConnect = HandleAccept;
                    channel.OnConnect?.Invoke(channel);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        /// <summary>
        /// 发送连接请求
        /// </summary>
        /// <returns></returns>
        public override async Task<ANetChannel> ConnectAsync()
        {
            var channel = new TcpChannel(endPoint, this);
            channel.OnConnect = HandleConnect;
            var isConnected = await channel.StartConnecting();
            if (!isConnected)
            {
                await ReConnecting(channel);
            }
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
                channel.StartRecv();
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
                channel.StartRecv();
                LogRecord.Log(LogLevel.Info, "HandleConnect", $"连接服务端:{channel.RemoteEndPoint}成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleConnect", e.ConvertToJson());
            }
        }
     }
}
