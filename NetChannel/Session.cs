
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace NetChannel
{
    public enum SessionType
    {
        None,
        Client,
        Server
    }

    public class Session
    {
        private ANetService netService;
        private IPEndPoint endPoint;
        private ANetChannel currentChannel;
        public const int HeartbeatTime = 8000;
        private DateTime LastCheckTime;
        private SessionType sessionType;

        /// <summary>
        /// 开始监听并接受客户端连接
        /// </summary>
        /// <param name="endPoint"></param>
        public async void Accept(IPEndPoint endPoint)
        {
            sessionType = SessionType.Server;
            this.endPoint = endPoint;
            netService = new TcpService(endPoint, this);
            netService.SendQueue.Start();
            await netService.AcceptAsync();
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        public async Task Connect(IPEndPoint endPoint)
        {
            sessionType = SessionType.Client;
            this.endPoint = endPoint;
            netService = new TcpService(endPoint, this);
            netService.SendQueue.Start();
            currentChannel = await netService.ConnectAsync();
        }

        /// <summary>
        /// 通知消息
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="packet"></param>
        public void Notice(ANetChannel channel, Packet packet)
        {
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
            packet.IsRpc = true;
            packet.RpcId = currentChannel.RpcId;
            currentChannel.AddRequest(packet, notificationAction);
            netService.SendQueue.Enqueue(new SendTask
            {
                Channel = currentChannel,
                Packet = packet,
            });
        }

        /// <summary>
        /// 发布消息
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
        internal async Task StartSend()
        {
            var channels = netService.Channels.Values;
            foreach (var channel in channels)
            {
                await channel.StartSend();
            }
        }

        /// <summary>
        /// 心跳检测
        /// </summary>
        internal void CheckHeadbeat()
        {
            var now = DateTime.Now;

            var lastCheckSpan = now - LastCheckTime;
            if(lastCheckSpan.TotalMilliseconds < HeartbeatTime)
            {
                return;
            }

            if(sessionType == SessionType.Client)
            {
                var timeSpan = now - currentChannel.LastSendHeartbeat;
                if (timeSpan.TotalMilliseconds > HeartbeatTime)
                {
                    SendMessage(new Packet
                    {
                        IsHeartbeat = true
                    });
                }
            }
            else if(sessionType == SessionType.Server)
            {
                var channels = netService.Channels.Values;
                foreach(var channel in channels)
                {
                    var timeSpan = now - channel.LastRecvHeartbeat;
                    if (timeSpan.TotalMilliseconds > HeartbeatTime)
                    {
                        channel.DisConnect();
                    }
                }
            }
            LastCheckTime = now;
        }
    }
}
