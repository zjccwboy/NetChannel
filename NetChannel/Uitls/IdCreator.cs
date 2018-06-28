using System.Threading;

namespace NetChannel
{
    public class IdCreator
    {
        private static int id;
        public static int CreateId()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 1, int.MaxValue);
            return id;
        }
    }

    public class KcpConnectSN
    {
        private static int id;
        public static int CreateSN()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 1, int.MaxValue);
            return id;
        }
    }
}
