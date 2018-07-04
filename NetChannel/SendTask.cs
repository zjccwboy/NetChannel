using System;
using System.Collections.Generic;
using System.Text;

namespace NetChannel
{
    /// <summary>
    /// 发送任务
    /// </summary>
    public class SendTask
    {
        /// <summary>
        /// 通讯管道对象
        /// </summary>
        public ANetChannel Channel { get; set; }

        /// <summary>
        /// 发送数据包
        /// </summary>
        public Packet Packet { get; set; }

        /// <summary>
        /// 将数据包写道发送缓冲区中
        /// </summary>
        public void WriteToBuffer()
        {
            Channel.WriteSendBuffer(Packet);
        }
    }
}
