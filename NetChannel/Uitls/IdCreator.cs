using System.Threading;

namespace NetChannel
{
    public class IdCreator
    {
        private static int id = 10000;
        public static int CreateId()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 10000, int.MaxValue);
            return id;
        }
    }

    public class KcpConnectSN
    {
        private static int id = 100000;
        public static int CreateSN()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 100000, int.MaxValue);
            return id;
        }
    }
}
