﻿
using System.Net;
using System.Threading.Tasks;
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

    public enum ProtocalType
    {
        Tcp,
        Kcp,
    }

    public class Session
    {
        public const int HeartbeatTime = 1000 * 20;

        private ANetService netService;
        private IPEndPoint endPoint;
        private ANetChannel currentChannel;
        private uint LastCheckTime;
        private SessionType sessionType;
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
        public async void Accept()
        {
            sessionType = SessionType.Server;
            if(protocalType == ProtocalType.Tcp)
            {
                netService = new TcpService(endPoint, this);
            }
            else if(protocalType == ProtocalType.Kcp)
            {
                netService = new KcpService(endPoint, this, sessionType);
            }
            netService.SendQueue.Start();
            await netService.AcceptAsync();
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        public async Task<ANetChannel> Connect()
        {
            sessionType = SessionType.Client;
            if (protocalType == ProtocalType.Tcp)
            {
                netService = new TcpService(endPoint, this);
            }
            else if (protocalType == ProtocalType.Kcp)
            {
                netService = new KcpService(endPoint, this, sessionType);
            }
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
            var now = TimeUitls.Now();

            var lastCheckSpan = now - LastCheckTime;
            if(lastCheckSpan < HeartbeatTime)
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
                if (timeSpan > HeartbeatTime)
                {
                    SendMessage(new Packet
                    {
                        IsHeartbeat = true
                    });
                    LogRecord.Log(LogLevel.Info, "CheckHeadbeat", $"发送心跳包到服务端:{currentChannel.RemoteEndPoint}...");
                }
            }
            else if(sessionType == SessionType.Server)
            {
                var channels = netService.Channels.Values;
                foreach(var channel in channels)
                {
                    var timeSpan = now - channel.LastRecvHeartbeat;
                    if (timeSpan > HeartbeatTime * 2)
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
