using System.Threading;

namespace NetChannel
{
    /// <summary>
    /// 通讯通道Id生成器，只适应TCP
    /// </summary>
    public class ChannelIdCreator
    {
        private static long id;
        public static uint CreateId()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 1, uint.MaxValue);
            return (uint)id;
        }
    }

    /// <summary>
    /// Actor对象Id生成器
    /// </summary>
    public class ActorMessageIdCreator
    {
        private static long id;
        public static uint CreateId()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 1, uint.MaxValue);
            return (uint)id;
        }
    }

    /// <summary>
    /// KCP连接确认号Conv生成器
    /// </summary>
    public class KcpConvIdCreator
    {
        private static int id = 100000;
        public static uint CreateId()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 100000, int.MaxValue);
            return (uint)id;
        }
    }
}
