
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using Common;

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
        public const int HeartbeatTime = 1000 * 20;
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
        public async Task<ANetChannel> Connect(IPEndPoint endPoint)
        {
            sessionType = SessionType.Client;
            this.endPoint = endPoint;
            netService = new TcpService(endPoint, this);
            netService.SendQueue.Start();
            currentChannel = await netService.ConnectAsync();
            return currentChannel;
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
            currentChannel.AddRequest(packet, notificationAction);
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
        public async Task StartSend()
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
        public void CheckHeadbeat()
        {
            var now = DateTime.Now;

            var lastCheckSpan = now - LastCheckTime;
            if(lastCheckSpan.TotalMilliseconds < HeartbeatTime)
            {
                return;
            }

            if(sessionType == SessionType.Client)
            {
                if(currentChannel == null)
                {
                    return;
                }
                if (!currentChannel.Connected)
                {
                    return;
                }
                //这块代码有bug，0字节检测为false时未必是断线
                //if (!currentChannel.CheckConnection())
                //{
                //    LogRecord.Log(LogLevel.Info, "CheckHeadbeat", $"与服务端:{currentChannel.DefaultEndPoint}失去连接");
                //    currentChannel.DisConnect();
                //}
                var timeSpan = now - currentChannel.LastSendHeartbeat;
                if (timeSpan.TotalMilliseconds > HeartbeatTime)
                {
                    SendMessage(new Packet
                    {
                        IsHeartbeat = true
                    });
                    LogRecord.Log(LogLevel.Info, "CheckHeadbeat", $"发送心跳包到服务端:{currentChannel.DefaultEndPoint}...");
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
                        LogRecord.Log(LogLevel.Info, "CheckHeadbeat", $"客户端:{channel.RemoteEndPoint}连接超时，心跳检测断开，心跳时长{timeSpan.TotalMilliseconds}...");
                        channel.DisConnect();
                    }
                }
            }
            LastCheckTime = now;
        }
    }
}
