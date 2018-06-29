using Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetChannel
{
    public class KcpService : ANetService
    {
        private readonly IPEndPoint endPoint;
        private UdpClient udpClient;
        private readonly PacketParser connectParser = new PacketParser(7);        

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="session"></param>
        public KcpService(IPEndPoint endPoint, Session session) : base(session)
        {
            sendQueue = new WorkQueue(session);
            this.endPoint = endPoint;
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
            if(this.udpClient != null)
            {
                return Task.CompletedTask;
            }

            this.udpClient = new UdpClient(this.endPoint);
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            this.udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);

            StartRecv();
            return Task.CompletedTask;
        }

        private async void StartRecv()
        {
            while (true)
            {
                UdpReceiveResult recvResult;
                try
                {
                    recvResult = await this.udpClient.ReceiveAsync();
                }
                catch (Exception e)
                {
                    LogRecord.Log(LogLevel.Warn, "StartRecv", e);
                    continue;
                }
                
                // accept
                uint conn = BitConverter.ToUInt32(recvResult.Buffer, 0);
                if (this.Channels.TryGetValue(conn, out ANetChannel channel))
                {
                    var kcpChannel = channel as KcpChannel;
                    kcpChannel.HandleRecv(recvResult);
                }
                else
                {
                    //丢弃非法数据包
                    if(recvResult.Buffer.Length != 7)
                    {
                        continue;
                    }

                    //客户端握手处理
                    connectParser.WriteBuffer(recvResult.Buffer, 0, recvResult.Buffer.Length);
                    var packet = connectParser.ReadBuffer();
                    if (packet.KcpProtocal == KcpNetProtocal.SYN)
                    {
                        HandleSYN(recvResult.RemoteEndPoint);
                    }
                    else if(packet.KcpProtocal == KcpNetProtocal.ACK)
                    {
                        HandleACK(packet, recvResult.RemoteEndPoint);
                    }
                    else if (packet.KcpProtocal == KcpNetProtocal.FIN)
                    {
                        HandleFIN(packet);
                    }
                }
            }
        }

        public override async Task<ANetChannel> ConnectAsync()
        {
            ConnectSender.SendSYN(this.udpClient, endPoint);
            var connected = false;
            tcs = new TaskCompletionSource<KcpChannel>();
            var cancellationToken = new System.Threading.CancellationTokenSource(3000);
            tcs.TrySetCanceled(cancellationToken.Token);
            var registration = cancellationToken.Token.Register(() =>
            {
                if (!connected)
                {
                    var kcpChannel = new KcpChannel(endPoint, this.udpClient, this);
                }
            });
            var channel = await tcs.Task;
            connected = channel.Connected;
            if (!channel.Connected)
            {
                await channel.ReConnecting();
            }
            return channel;
        }

        private void HandleSYN(IPEndPoint endPoint)
        {
            var conv = KcpConvIdCreator.CreateId();
            while (this.Channels.ContainsKey(conv))
            {
                conv = KcpConvIdCreator.CreateId();
            }
            var channel = new KcpChannel(endPoint, this.udpClient, this, conv);
            channel.OnConnect = DoAccept;
            ConnectSender.SendACK(this.udpClient, endPoint, channel);
        }

        private TaskCompletionSource<KcpChannel> tcs;
        private void HandleACK(Packet packet, IPEndPoint endPoint)
        {
            var channel = new KcpChannel(endPoint, this.udpClient, this, packet.ActorMessageId);
            channel.OnConnect = DoConnect;
            channel.InitKcp();
            channel.OnConnect?.Invoke(channel);
            if (tcs != null)
            {
                var connTcs = tcs;
                tcs = null;
                connTcs.SetResult(channel);
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
                //channel.StartRecv();
                LogRecord.Log(LogLevel.Info, "DoAccept", $"连接服务端:{channel.RemoteEndPoint}成功...");
            }
            catch (Exception e)
            {
                LogRecord.Log(LogLevel.Warn, "DoConnect", e);
            }
        }
    }
}
