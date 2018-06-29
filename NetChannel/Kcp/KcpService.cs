using Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace NetChannel
{
    /// <summary>
    /// KCP通讯服务
    /// </summary>
    public class KcpService : ANetService
    {
        private UdpClient udpClient;
        private readonly PacketParser connectParser = new PacketParser(7);
        private KcpChannel currentChannel;
        private IPEndPoint endPoint;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="session"></param>
        public KcpService(IPEndPoint endPoint, Session session, SessionType sessionType) : base(session)
        {
            this.endPoint = endPoint;
            sendQueue = new WorkQueue(session);
            if(sessionType == SessionType.Server)
            {
                this.udpClient = new UdpClient(endPoint);
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                this.udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
            }
            else if(sessionType == SessionType.Client)
            {
                this.udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            }
            StartRecv();
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
        public override Task AcceptAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 发送连接请求
        /// </summary>
        /// <returns></returns>
        public override async Task<ANetChannel> ConnectAsync()
        {
            ConnectSender.SendSYN(this.udpClient, endPoint);
            var connected = false;
            //var cancellationToken = new System.Threading.CancellationTokenSource(3000);
            //var registration = cancellationToken.Token.Register(() =>
            //{
            //    if (!connected)
            //    {
            //        var kcpChannel = new KcpChannel(endPoint, this.udpClient, this);
            //        tcs.TrySetResult(kcpChannel);
            //    }
            //});
            tcs = new TaskCompletionSource<KcpChannel>();
            var channel = await tcs.Task;
            currentChannel = channel;
            connected = channel.Connected;
            if (!channel.Connected)
            {
                await channel.ReConnecting();
            }
            return channel;
        }

        /// <summary>
        /// 开始接收数据包
        /// </summary>
        private async void StartRecv()
        {
            while (true)
            {
                UdpReceiveResult recvResult;
                try
                {
                    recvResult = await this.udpClient.ReceiveAsync();
                    LogRecord.Log(LogLevel.Error, "StartRecv", $"收到远程电脑:{recvResult.RemoteEndPoint}");
                }
                catch (Exception e)
                {
                    LogRecord.Log(LogLevel.Warn, "StartRecv", e);
                    continue;
                }

                if (recvResult.Buffer.Length == 3 || recvResult.Buffer.Length == 7)
                {
                    //客户端握手处理
                    connectParser.WriteBuffer(recvResult.Buffer, 0, recvResult.Buffer.Length);
                    var packet = connectParser.ReadBuffer();
                    if (!packet.IsSuccess)
                    {
                        //丢弃非法数据包
                        connectParser.Buffer.Flush();
                        continue;
                    }
                    if (packet.KcpProtocal == KcpNetProtocal.SYN)
                    {
                        HandleSYN(recvResult);
                    }
                    else if (packet.KcpProtocal == KcpNetProtocal.ACK)
                    {
                        HandleACK(packet, recvResult);
                    }
                    else if (packet.KcpProtocal == KcpNetProtocal.FIN)
                    {
                        HandleFIN(packet);
                    }
                }
                else
                {
                    uint connectConv = BitConverter.ToUInt32(recvResult.Buffer, 0);
                    if (this.Channels.TryGetValue(connectConv, out ANetChannel channel))
                    {
                        var kcpChannel = channel as KcpChannel;
                        kcpChannel.HandleRecv(recvResult);
                    }
                }
            }
        }

        /// <summary>
        /// 处理客户端SYN连接请求
        /// </summary>
        /// <param name="recvResult"></param>
        private void HandleSYN(UdpReceiveResult recvResult)
        {
            var conv = KcpConvIdCreator.CreateId();
            while (this.Channels.ContainsKey(conv))
            {
                conv = KcpConvIdCreator.CreateId();
            }
            var channel = new KcpChannel(recvResult, this.udpClient, this, conv);
            channel.OnConnect = HandleAccept;
            channel.InitKcp();
            channel.OnConnect?.Invoke(channel);
            ConnectSender.SendACK(this.udpClient, channel.RemoteEndPoint, channel);
        }

        private TaskCompletionSource<KcpChannel> tcs;
        /// <summary>
        /// 处理连接请求ACK应答
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="recvResult"></param>
        private void HandleACK(Packet packet, UdpReceiveResult recvResult)
        {
            var channel = new KcpChannel(recvResult,this.udpClient, this, packet.ActorMessageId);
            channel.OnConnect = HandleConnect;
            channel.InitKcp();
            channel.OnConnect?.Invoke(channel);
            if (tcs != null)
            {
                var connTcs = tcs;
                tcs = null;
                if (!connTcs.Task.IsCompleted)
                {
                    connTcs.SetResult(channel);
                }
            }
        }

        /// <summary>
        /// 处理连接断开FIN请求
        /// </summary>
        /// <param name="packet"></param>
        private void HandleFIN(Packet packet)
        {
            if (this.Channels.TryGetValue(packet.ActorMessageId, out ANetChannel channel))
            {
                if (channel.Connected)
                {
                    channel.Connected = false;
                    channel.OnDisConnect?.Invoke(channel);
                }
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
                LogRecord.Log(LogLevel.Info, "HandleAccept", $"接受客户端:{channel.RemoteEndPoint}连接成功...");
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
                LogRecord.Log(LogLevel.Info, "HandleConnect", $"连接服务端:{channel.RemoteEndPoint}成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "HandleConnect", e);
            }
        }
    }
}
