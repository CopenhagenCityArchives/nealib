using NEA.ArchiveModel;
using System;
using System.IO.Abstractions.TestingHelpers;

namespace NEA.Testing
{
    public class ArchiveFileSystemFactory
    {
        private readonly MockFileSystem _fileSystem;
        private readonly string _basePath;
        public ArchiveFileSystemFactory(string basePath)
        {
            _fileSystem = new MockFileSystem();
            _basePath = basePath;
        }
        public IArchiveFactory AddArchive(string name, AVRuleSet type)
        {
            switch (type)
            {
                case AVRuleSet.BKG1007:
                    return new Archive1007Factory(this, _fileSystem, _basePath, name);
                case AVRuleSet.BKG342:
                case AVRuleSet.BKG128:
                default:
                    throw new NotSupportedException("This ArchiveVersion type is currently not supported");
            }

        }
        public MockFileSystem Done()
        {
            return _fileSystem;
        }

    }

}
