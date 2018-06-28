using System;
using System.Collections.Generic;
using System.Text;

namespace NetChannel
{
    public static class TimeUitls
    {
        private static readonly long epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        public static long ClientNow()
        {
            return (DateTime.UtcNow.Ticks - epoch) / 10000;
        }

        public static uint Now()
        {
            return (uint)ClientNow();
        }
    }
}
