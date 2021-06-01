using NUnit.Framework;
using NEA.Helpers;
using System.IO.Abstractions;
using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Collections.Generic;

namespace NEA.Testing.Helpers
{
    [TestFixture]
    public class ArchiveversionIdentifierTests
    {
        [Test]
        public void NonExistingFolder_ThrowException()
        {
            var mockFileSystem = new MockFileSystem();
            mockFileSystem.AddDirectory(@"C:\");

            ArchiveVersionIdentifier avId = new ArchiveVersionIdentifier(mockFileSystem);
            
            Assert.Throws<DirectoryNotFoundException>(() => { avId.GetArchiveVersionInfosInFolder(@"C:\Archives");  }, "Should throw exception when directory does not exist");
        }

        [Test]
        public void NoAvInFolder_ReturnEmptyList()
        {
            var mockFileSystem = new MockFileSystem();
            mockFileSystem.AddDirectory(@"C:\emptyDir");

            ArchiveVersionIdentifier avId = new ArchiveVersionIdentifier(mockFileSystem);

            Assert.IsEmpty(avId.GetArchiveVersionInfosInFolder(@"C:\emptyDir"));
        }

        [Test]
        [TestCase(@"C:\Archives\AVID.KSA.1\AVID.KSA.1.1")]
        [TestCase(@"C:\Archives\AVID.KSA.1")]
        public void AvInFolder_ReturnavListWithMedia(string path)
        {
            IFileSystem mockFileSystem = new ArchiveFileSystemFactory(@"C:\Archives")
                .AddArchive("AVID.KSA.1", ArchiveModel.AVRuleSet.BKG1007)
                .Done();

            ArchiveVersionIdentifier avId = new ArchiveVersionIdentifier(mockFileSystem);
            var avList = avId.GetArchiveVersionInfosInFolder(path);

            Assert.IsNotEmpty(avList);
            Assert.IsNotEmpty(avList[0].Info.Medias);
        }

        [Test]
        public void AVInMultipleFolders_ReturnavListWithMedias()
        {
            var mockFileSystem = new MockFileSystem();
            mockFileSystem.AddDirectory(@"C:\AVID.KSA.1\AVID.KSA.1.2\Documents");
            mockFileSystem.AddDirectory(@"D:\AVID.KSA.1.1\Indices");


            ArchiveVersionIdentifier avId = new ArchiveVersionIdentifier(mockFileSystem);
            var avList = avId.GetArchiveVersionInfosInFolders(new List<string>(){ @"C:\", @"D:\" });

            Assert.IsNotEmpty(avList);

            Assert.AreEqual(2, avList[0].Info.Medias.Count);
            
            Assert.IsTrue(avList[0].Info.Medias[0].MediaFolderName == "AVID.KSA.1.1");
            Assert.IsTrue(avList[0].Info.Medias[1].MediaFolderName == "AVID.KSA.1.2");
        }

        [Test]
        public void AVInSubSubFolder_ReturnAVListWithMedias()
        {
            var mockFileSystem = new MockFileSystem();
            mockFileSystem.AddDirectory(@"C:\Folder\Subfolder\AVID.KSA.1.1\Indices");


            ArchiveVersionIdentifier avId = new ArchiveVersionIdentifier(mockFileSystem);
            var avList = avId.GetArchiveVersionInfosInFolders(new List<string>() { @"C:\", }, 3, null);

            Assert.IsNotEmpty(avList);

            Assert.AreEqual(2, avList[0].Info.Medias.Count);

            Assert.IsTrue(avList[0].Info.Medias[0].MediaFolderName == "AVID.KSA.1.1");
            Assert.IsTrue(avList[0].Info.Medias[1].MediaFolderName == "AVID.KSA.1.2");
        }
    }
}
