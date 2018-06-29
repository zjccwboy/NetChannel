using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetChannel
{
    public class ConnectSender
    {
        /// <summary>
        /// 发送SYN连接请求
        /// </summary>
        /// <param name="udpClient"></param>
        /// <param name="endPoint"></param>
        public static void SendSYN(UdpClient udpClient, IPEndPoint endPoint)
        {
            //发送SYN包
            var synPacket = new Packet
            {
                KcpProtocal = KcpNetProtocal.SYN,
            };

            //握手包不要经过KCP发送
            var bytes = synPacket.GetHeadBytes();
            udpClient.Send(bytes, bytes.Length, endPoint);
        }

        /// <summary>
        /// 发送ACK应答请求
        /// </summary>
        /// <param name="udpClient"></param>
        /// <param name="endPoint"></param>
        /// <param name="channel"></param>
        public static void SendACK(UdpClient udpClient, IPEndPoint endPoint, KcpChannel channel)
        {
            var finPacket = new Packet
            {
                KcpProtocal = KcpNetProtocal.FIN,
                IsActorMessage = true,
                ActorMessageId = channel.Id,
            };

            //握手包不经过KCP发送
            var bytes = finPacket.GetHeadBytes();
            udpClient.Send(bytes, bytes.Length, endPoint);
        }

        /// <summary>
        /// 发送FIN连接断开请求
        /// </summary>
        /// <param name="udpClient"></param>
        /// <param name="endPoint"></param>
        /// <param name="channel"></param>
        private static void SendFIN(UdpClient udpClient, IPEndPoint endPoint, KcpChannel channel)
        {
            var finPacket = new Packet
            {
                KcpProtocal = KcpNetProtocal.FIN,
                IsActorMessage = true,
                ActorMessageId = channel.Id,
            };

            //握手包不经过KCP发送
            var bytes = finPacket.GetHeadBytes();
            udpClient.Send(bytes, bytes.Length, endPoint);
        }
    }
}
