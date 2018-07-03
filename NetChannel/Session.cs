
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
            sessionType = NetServiceType.Server;
            if(protocalType == ProtocalType.Tcp)
            {
                netService = new TcpService(endPoint, this);
            }
            else if(protocalType == ProtocalType.Kcp)
            {
                netService = new KcpService(endPoint, this, sessionType);
            }
            netService.Accept();
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        public ANetChannel Connect()
        {
            sessionType = NetServiceType.Client;
            if (protocalType == ProtocalType.Tcp)
            {
                netService = new TcpService(endPoint, this);
            }
            else if (protocalType == ProtocalType.Kcp)
            {
                netService = new KcpService(endPoint, this, sessionType);
            }
            currentChannel = netService.Connect();
            return currentChannel;
        }

        public void Start()
        {
            netService.SendQueue.Start();
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

            netService.SendQueue.Enqueue(new SendTask
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
            if (!currentChannel.Connected)
            {
                return;
            }

            packet.IsRpc = true;
            packet.RpcId = currentChannel.RpcId;
            currentChannel.AddPacket(packet, notificationAction);
            netService.SendQueue.Enqueue(new SendTask
            {
                Channel = currentChannel,
                Packet = packet,
            });
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="packet"></param>
        public void SendMessage(Packet packet)
        {
            netService.SendQueue.Enqueue(new SendTask
            {
                Channel = this.currentChannel,
                Packet = packet,
            });
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <returns></returns>
        internal async void StartSend()
        {
            var channels = netService.Channels.Values;
            foreach (var channel in channels)
            {
                channel.StartSend();
            }
        }

        /// <summary>
        /// 心跳检测
        /// </summary>
        internal void CheckHeadbeat()
        {
            var now = TimeUitls.Now();

            var lastCheckSpan = now - LastCheckTime;
            if(lastCheckSpan < HeartbeatTime)
            {
                return;
            }

            if(sessionType == NetServiceType.Client)
            {
                if(currentChannel == null)
                {
                    return;
                }
                if (!currentChannel.Connected)
                {
                    return;
                }
                var timeSpan = now - currentChannel.LastSendTime;
                if (timeSpan > HeartbeatTime)
                {
                    SendMessage(new Packet
                    {
                        IsHeartbeat = true
                    });
                    LogRecord.Log(LogLevel.Info, "CheckHeadbeat", $"发送心跳包到服务端:{currentChannel.RemoteEndPoint}...");
                }
            }
            else if(sessionType == NetServiceType.Server)
            {
                var channels = netService.Channels.Values;
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
