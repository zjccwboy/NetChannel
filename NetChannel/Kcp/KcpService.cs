using Common;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetChannel
{
    /// <summary>
    /// KCP通讯服务
    /// </summary>
    public class KcpService : ANetService
    {
        private readonly PacketParser connectParser = new PacketParser(7);
        private IPEndPoint endPoint;
        private EndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 0);
        private readonly byte[] recvBytes = new byte[1400];

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="session"></param>
        public KcpService(IPEndPoint endPoint, Session session, NetServiceType serviceType) : base(session)
        {
            this.serviceType = serviceType;
            this.endPoint = endPoint;
        }
        
        /// <summary>
        /// 开始监听并接受连接请求
        /// </summary>
        /// <returns></returns>
        public override void Accept()
        {
            if(this.acceptor == null)
            {
                this.acceptor = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                if (serviceType == NetServiceType.Server)
                {
                    uint IOC_IN = 0x80000000;
                    uint IOC_VENDOR = 0x18000000;
                    uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                    this.acceptor.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
                }
                acceptor.Bind(this.endPoint);
            }
        }

        /// <summary>
        /// 发送连接请求
        /// </summary>
        /// <returns></returns>
        public override ANetChannel Connect()
        {
            if(this.acceptor == null)
            {
                this.acceptor = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                this.ClientChannel = new KcpChannel(this.acceptor, this.endPoint, this, 1000);
                this.ClientChannel.StartConnecting();
            }
            return this.ClientChannel;
        }

        /// <summary>
        /// 更新发送接收队列
        /// </summary>
        public override void Update()
        {
            if (serviceType == NetServiceType.Client)
            {
                if(ClientChannel != null)
                {
                    this.ClientChannel.StartConnecting();
                }
            }
            this.HandleSend();
            this.StartRecv();
        }

        //int recvCouut = 0;
        //int startCount = 0;
        /// <summary>
        /// 开始接收数据包
        /// </summary>
        public void StartRecv()
        {
            int recvCount = 0;
            try
            {
                if(this.acceptor.Available == 0)
                {
                    return;
                }

                recvCount = this.acceptor.ReceiveFrom(recvBytes, SocketFlags.None, ref this.ipEndPoint);
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "StartRecv", e);
                return;
            }

            if (recvCount == 3 || recvCount == 7)
            {
                //握手处理
                connectParser.WriteBuffer(recvBytes, 0, recvCount);
                var packet = connectParser.ReadBuffer();
                if (!packet.IsSuccess)
                {
                    LogRecord.Log(LogLevel.Error, "StartRecv", $"丢弃非法数据包:{this.acceptor.RemoteEndPoint}.");
                    //丢弃非法数据包
                    connectParser.Buffer.Flush();
                    return;
                }
                if (packet.KcpProtocal == KcpNetProtocal.SYN)
                {
                    HandleSYN(this.acceptor, this.ipEndPoint as IPEndPoint);
                }
                else if (packet.KcpProtocal == KcpNetProtocal.ACK)
                {
                    HandleACK(packet, this.acceptor, this.ipEndPoint as IPEndPoint);
                }
                else if (packet.KcpProtocal == KcpNetProtocal.FIN)
                {
                    HandleFIN(packet, this.ipEndPoint as IPEndPoint);
                }
            }
            else
            {
                uint connectConv = BitConverter.ToUInt32(recvBytes, 0);
                if (this.Channels.TryGetValue(connectConv, out ANetChannel channel))
                {
                    channel.HandleRecv(recvBytes, 0, recvCount);
                    channel.StartRecv();
                }
                else
                {
                    LogRecord.Log(LogLevel.Notice, "数据包异常", connectConv.ToString());
                }
            }
        }

        /// <summary>
        /// 处理客户端SYN连接请求
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="remoteEP"></param>
        private void HandleSYN(Socket socket, EndPoint remoteEP)
        {
            var conv = KcpConvIdCreator.CreateId();
            while (this.Channels.ContainsKey(conv))
            {
                conv = KcpConvIdCreator.CreateId();
            }
            var channel = new KcpChannel(socket, remoteEP as IPEndPoint, this, conv);
            channel.InitKcp();
            channel.OnConnect = HandleAccept;
            channel.OnConnect?.Invoke(channel);
            ConnectSender.SendACK(this.acceptor, channel.RemoteEndPoint, conv);
        }

        /// <summary>
        /// 处理连接请求ACK应答
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="socket"></param>
        /// <param name="remoteEP"></param>
        private void HandleACK(Packet packet, Socket socket, EndPoint remoteEP)
        {
            if(this.ClientChannel == null)
            {
                return;
            }
            var channel = this.ClientChannel as KcpChannel;
            channel.RemoteEndPoint = remoteEP as IPEndPoint;
            channel.Id = packet.ActorMessageId;
            channel.InitKcp();
            channel.OnConnect = HandleConnect;
            channel.OnConnect?.Invoke(this.ClientChannel);
        }

        /// <summary>
        /// 处理连接断开FIN请求
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="remoteEP"></param>
        private void HandleFIN(Packet packet, EndPoint remoteEP)
        {
            if (this.Channels.TryGetValue(packet.ActorMessageId, out ANetChannel channel))
            {
                channel.Connected = false;
                channel.OnDisConnect?.Invoke(channel);
            }
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
                LogRecord.Log(LogLevel.Info, "HandleAccept", $"接受客户端:{channel.RemoteEndPoint}连接成功.");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleAccept", e);
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
                LogRecord.Log(LogLevel.Info, "HandleConnect", $"连接服务端:{channel.RemoteEndPoint}成功.");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleConnect", e);
            }
        }
    }
}
