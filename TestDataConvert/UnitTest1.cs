using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;
using NetChannel;
using System.Text;

namespace TestDataConvert
{
    public class TestClass
    {
        public string TestString { get; set; }
        public int TestInt { get; set; }
        public DateTime TestDateTime { get; set; }

        public override bool Equals(object obj)
        {
            var testb = obj as TestClass;
            return TestString == testb.TestString && TestInt == testb.TestInt && TestDateTime == testb.TestDateTime;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestObjectConvertToJson()
        {
            var t = "t";
            var classa = new TestClass
            {
                TestString = "TestString",
                TestInt = 1000,
                TestDateTime = DateTime.Now,
            };

            var json = classa.ConvertToJson();
            var classb = json.ConvertToObject<TestClass>();

            Assert.AreEqual(classa, classb);

            var bytes = classa.ConvertToBytes();
            var classc = bytes.ConvertToObject<TestClass>();

            Assert.AreEqual(classa, classc);
        }       
    }
}
