
using System.Net;
using System.Threading.Tasks;
using System;
using Common;

namespace NetChannel
{
    /// <summary>
    /// 服务类型
    /// </summary>
    public enum NetServiceType
    {
        None,
        Client,
        Server
    }

    /// <summary>
    /// 协议类型
    /// </summary>
    public enum ProtocalType
    {
        Tcp,
        Kcp,
    }

    /// <summary>
    /// 通讯会话接口类
    /// </summary>
    public class Session
    {
        private ANetService netService;
        private IPEndPoint endPoint;
        private ANetChannel clientChannel;
        private ProtocalType protocalType;

        public Session(IPEndPoint endPoint, ProtocalType protocalType)
        {
            this.endPoint = endPoint;
            this.protocalType = protocalType;
        }

        /// <summary>
        /// 开始监听并接受客户端连接
        /// </summary>
        /// <param name="endPoint"></param>
        public void Accept()
        {
            if(this.protocalType == ProtocalType.Tcp)
            {
                this.netService = new TcpService(this.endPoint, this, NetServiceType.Server);
            }
            else if(this.protocalType == ProtocalType.Kcp)
            {
                this.netService = new KcpService(this.endPoint, this, NetServiceType.Server);
            }
            this.netService.Accept();
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        public ANetChannel Connect()
        {
            if (this.protocalType == ProtocalType.Tcp)
            {
                this.netService = new TcpService(this.endPoint, this, NetServiceType.Client);
            }
            else if (this.protocalType == ProtocalType.Kcp)
            {
                this.netService = new KcpService(this.endPoint, this, NetServiceType.Client);
            }
            clientChannel = this.netService.Connect();
            return clientChannel;
        }

        public void Update()
        {
            try
            {
                OneThreadSynchronizationContext.Instance.Update();
                this.netService.Update();
            }
            catch(Exception e)
            {
#if DEBUG
                LogRecord.Log(LogLevel.Warn, "Update", e);
#endif
            }
        }

        /// <summary>
        /// 通知消息
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="packet"></param>
        public void Notice(ANetChannel channel, Packet packet)
        {
            if (!channel.Connected)
            {
                return;
            }

            this.netService.Enqueue(new SendTask
            {
                Channel = channel,
                Packet = packet,
            });
        }

        /// <summary>
        /// 订阅消息
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="packet"></param>
        /// <param name="notificationAction"></param>
        public void Subscribe(Packet packet, Action<Packet> notificationAction)
        {
            if (!this.clientChannel.Connected)
            {
                return;
            }

            packet.IsRpc = true;
            packet.RpcId = this.clientChannel.RpcId;
            this.clientChannel.AddPacket(packet, notificationAction);
            this.netService.Enqueue(new SendTask
            {
                Channel = this.clientChannel,
                Packet = packet,
            });
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="packet"></param>
        public void SendMessage(Packet packet)
        {
            this.netService.Enqueue(new SendTask
            {
                Channel = this.clientChannel,
                Packet = packet,
            });
        }       
    }
}
