using System;
using System.Collections.Generic;
using HardHorn.Archiving;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibHardHornTest
{
    [TestClass]
    public class TiffTest
    {
        [TestMethod]
        [DeploymentItem(@"..\..\TestResources", @"TestResources")]
        public void TestTiffLoad()
        {
            var tiff = Tiff.Open(@"TestResources\test2.tif");
            Assert.AreEqual(ByteOrder.LittleEndian, tiff.ByteOrder);

            uint offset = tiff.FirstImageFileDirectoryOffset;
            ImageFileDirectory ifd;
            do
            {
                ifd = tiff.ReadImageFileDirectory(offset);
                if (ifd.NextImageFileDirectoryOffset.HasValue)
                    offset = ifd.NextImageFileDirectoryOffset.Value;

            } while (!ifd.LastImageFileDirectory);
        }
    }
}
