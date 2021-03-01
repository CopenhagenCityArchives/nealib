using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using NEA.ArchiveModel.BKG1007;
using System.IO;

namespace NEA.Testing.src
{
    [TestClass]
    public class ModelTest
    {
        [TestMethod]
        public void sandbox()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(fileIndexType));
            using (var stream = File.OpenRead(@"C:\Archives\AVID.SA.18004.1\Indices\fileIndex.xml"))
            {
                var fileindex = serializer.Deserialize(stream);
                Assert.IsNotNull(fileindex);
            }
        }
    }
}
