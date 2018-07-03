
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
        public const int HeartbeatTime = 1000 * 20;

        private ANetService netService;
        private IPEndPoint endPoint;
        private ANetChannel currentChannel;
        private uint LastCheckTime;
        private NetServiceType sessionType;
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
            this.sessionType = NetServiceType.Server;
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
            this.sessionType = NetServiceType.Client;
            if (this.protocalType == ProtocalType.Tcp)
            {
                this.netService = new TcpService(this.endPoint, this, NetServiceType.Client);
            }
            else if (this.protocalType == ProtocalType.Kcp)
            {
                this.netService = new KcpService(this.endPoint, this, NetServiceType.Client);
            }
            currentChannel = this.netService.Connect();
            return currentChannel;
        }

        public void Update()
        {
            OneThreadSynchronizationContext.Instance.Update();
            this.netService.Update();
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

            this.netService.SendQueue.Enqueue(new SendTask
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
            if (!this.currentChannel.Connected)
            {
                return;
            }

            packet.IsRpc = true;
            packet.RpcId = this.currentChannel.RpcId;
            this.currentChannel.AddPacket(packet, notificationAction);
            this.netService.SendQueue.Enqueue(new SendTask
            {
                Channel = this.currentChannel,
                Packet = packet,
            });
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="packet"></param>
        public void SendMessage(Packet packet)
        {
            this.netService.SendQueue.Enqueue(new SendTask
            {
                Channel = this.currentChannel,
                Packet = packet,
            });
        }

        /// <summary>
        /// 心跳检测
        /// </summary>
        internal void CheckHeadbeat()
        {
            var now = TimeUitls.Now();

            var lastCheckSpan = now - this.LastCheckTime;
            if(lastCheckSpan < HeartbeatTime)
            {
                return;
            }

            if(this.sessionType == NetServiceType.Client)
            {
                if(this.currentChannel == null)
                {
                    return;
                }
                if (!this.currentChannel.Connected)
                {
                    return;
                }
                var timeSpan = now - this.currentChannel.LastSendTime;
                if (timeSpan > HeartbeatTime)
                {
                    SendMessage(new Packet
                    {
                        IsHeartbeat = true
                    });
                    LogRecord.Log(LogLevel.Info, "CheckHeadbeat", $"发送心跳包到服务端:{this.currentChannel.RemoteEndPoint}...");
                }
            }
            else if(this.sessionType == NetServiceType.Server)
            {
                var channels = this.netService.Channels.Values;
                foreach(var channel in channels)
                {
                    var timeSpan = now - channel.LastRecvTime;
                    if (timeSpan > HeartbeatTime * 2000)
                    {
                        LogRecord.Log(LogLevel.Info, "CheckHeadbeat", $"客户端:{channel.RemoteEndPoint}连接超时，心跳检测断开，心跳时长{timeSpan}...");
                        channel.DisConnect();
                    }
                }
            }
            LastCheckTime = now;
        }
    }
}
