using Microsoft.VisualStudio.TestTools.UnitTesting;
using NEA.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEA.Testing.HelperTests
{
    [TestClass]
    public class ByteHelperTest
    {
        [TestMethod]
        public void CanReverseByteString()
        {
            var buffer = new RandomBufferGenerator(0x100000);
            var bytes = buffer.GenerateBufferFromSeed(32);
            var byteString = BitConverter.ToString(bytes).Replace("-", "");
            var parsedByteString = ByteHelper.ParseHex(byteString);

            for (int i = 0; i < bytes.Length; i++)
            {
                Assert.AreEqual(bytes[i], parsedByteString[i]);
            }
        }
    }
}
