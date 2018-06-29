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
        internal override WorkQueue SendQueue
        {
            get
            {
                return sendQueue;
            }
        }

        public override Task AcceptAsync()
        {
            return Task.CompletedTask;
        }

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
                        connectParser.Buffer.UpdateRead(connectParser.Buffer.DataSize);
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

        private void HandleSYN(UdpReceiveResult recvResult)
        {
            var conv = KcpConvIdCreator.CreateId();
            while (this.Channels.ContainsKey(conv))
            {
                conv = KcpConvIdCreator.CreateId();
            }
            var channel = new KcpChannel(recvResult, this.udpClient, this, conv);
            channel.OnConnect = DoAccept;
            channel.InitKcp();
            channel.OnConnect?.Invoke(channel);
            ConnectSender.SendACK(this.udpClient, channel.RemoteEndPoint, channel);
        }

        private TaskCompletionSource<KcpChannel> tcs;
        private void HandleACK(Packet packet, UdpReceiveResult recvResult)
        {
            var channel = new KcpChannel(recvResult,this.udpClient, this, packet.ActorMessageId);
            channel.OnConnect = DoConnect;
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

        private void DoAccept(ANetChannel channel)
        {
            try
            {
                channel.OnDisConnect = DoDisConnectOnServer;
                channel.Connected = true;
                AddChannel(channel);
                AddHandler(channel);
                LogRecord.Log(LogLevel.Info, "DoAccept", $"接受客户端:{channel.RemoteEndPoint}连接成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "DoAccept", e);
            }
        }

        protected void DoConnect(ANetChannel channel)
        {
            try
            {
                channel.OnDisConnect = DoDisConnectOnClient;
                channel.Connected = true;
                AddChannel(channel);
                AddHandler(channel);
                LogRecord.Log(LogLevel.Info, "DoAccept", $"连接服务端:{channel.RemoteEndPoint}成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "DoConnect", e);
            }
        }
    }
}
