using System.Threading;

namespace NetChannel
{
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

    public class KcpConvIdCreator
    {
        private static long id = 100000;
        public static uint CreateId()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 100000, uint.MaxValue);
            return (uint)id;
        }
    }
}
