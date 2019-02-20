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
            var tiff = Tiff.Open(@"TestResources\tiff\2.tif");
            Assert.AreEqual(ByteOrder.LittleEndian, tiff.ByteOrder);

            uint offset = tiff.FirstImageFileDirectoryOffset;
            ImageFileDirectory ifd;
            while ((ifd = tiff.ReadNextImageFileDirectory()) != null)
            {
                Console.WriteLine(ifd.ToString());
                foreach (var entry in ifd.Entries.Values)
                {
                    Console.WriteLine($"\t{entry.ToString()}");
                    if (entry.IsValueReference())
                    {
                        var value = tiff.ReadImageFileDirectoryEntryReferencedValue(entry);
                        if (value.GetType().IsArray)
                        {
                            Console.WriteLine($"\t\tReferenced Values={string.Join(", ", (Array)value)}");
                        }
                        else
                            Console.WriteLine($"\t\tReferenced Value={value.ToString()}");
                    }
                    else
                    {
                        Console.WriteLine($"\t\tValue={entry.Value}");
                    }
                }
            }
        }
    }
}
