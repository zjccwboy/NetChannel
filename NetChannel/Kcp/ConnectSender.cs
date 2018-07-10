using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetChannel
{
    /// <summary>
    /// 连接发送辅助类
    /// </summary>
    public class ConnectSender
    {
        /// <summary>
        /// 发送SYN连接请求
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="endPoint"></param>
        public static void SendSYN(Socket socket, IPEndPoint endPoint)
        {
            //发送SYN包
            var synPacket = new Packet
            {
                KcpProtocal = KcpNetProtocal.SYN,
            };

            //握手包不要经过KCP发送
            var bytes = synPacket.GetHeadBytes();
            socket.SendTo(bytes, 0, bytes.Length,SocketFlags.None,  endPoint);
        }

        /// <summary>
        /// 发送ACK应答请求
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="endPoint"></param>
        /// <param name="conv"></param>
        public static void SendACK(Socket socket, IPEndPoint endPoint, uint conv)
        {
            var finPacket = new Packet
            {
                KcpProtocal = KcpNetProtocal.ACK,
                IsActorMessage = true,
                ActorMessageId = conv,
            };

            //握手包不经过KCP发送
            var bytes = finPacket.GetHeadBytes();
            socket.SendTo(bytes, bytes.Length, SocketFlags.None,  endPoint);
        }

        /// <summary>
        /// 发送FIN连接断开请求
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="endPoint"></param>
        /// <param name="conv"></param>
        public static void SendFIN(Socket socket, IPEndPoint endPoint, uint conv)
        {
            var finPacket = new Packet
            {
                KcpProtocal = KcpNetProtocal.FIN,
                IsActorMessage = true,
                ActorMessageId = conv,
            };

            //握手包不经过KCP发送
            var bytes = finPacket.GetHeadBytes();
            socket.SendTo(bytes, bytes.Length, SocketFlags.None, endPoint);
        }
    }
}
