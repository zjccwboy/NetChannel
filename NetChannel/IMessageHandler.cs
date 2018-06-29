
namespace NetChannel
{
    /// <summary>
    /// 消息处理接口，应该所有的消息类都应该从该接口派生实现
    /// </summary>
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
