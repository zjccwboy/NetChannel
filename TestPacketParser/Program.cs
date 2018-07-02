using NetChannel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPacketParser
{
    class Program
    {
        static void Main(string[] args)
        {
            TestPackParser();
            Console.Read();
        }

        static void TestPackParser()
        {
            var parser = new PacketParser();
            var packet = new Packet
            {
                Data = Encoding.UTF8.GetBytes("111111111122222222223333333333444444444455555555556666666666777777777788888888889999999999")
            };

            for(var j=1; j <= 1000; j++)
            {
                var dataCount = 100000;
                for (var i = 0; i < dataCount; i++)
                {
                    parser.WriteBuffer(packet);
                }

                var count = 0;
                while (true)
                {
                    var result = parser.ReadBuffer();
                    if (result.IsSuccess)
                    {
                        var str = Encoding.UTF8.GetString(result.Data);
                        if (str != "111111111122222222223333333333444444444455555555556666666666777777777788888888889999999999")
                        {
                            Console.WriteLine(str);
                        }
                        count++;
                        if (count == dataCount)
                        {
                            Console.WriteLine($"完成第{j}次");
                            break;
                        }
                    }

                }
            }            
        }
    }
}
