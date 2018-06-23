
namespace NetChannel
{
    public interface IMessageHandler
    {
        /// <summary>
        /// Channel
        /// </summary>
        ANetChannel Channel { get; set; }
        /// <summary>
        /// NetService
        /// </summary>
        ANetService NetService { get; set; }
        /// <summary>
        /// 处理接收消息
        /// </summary>
        /// <param name="packet"></param>
        void DoReceive(Packet packet);
    }
}
