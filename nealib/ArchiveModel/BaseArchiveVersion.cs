using log4net;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NEA.ArchiveModel
{
    public abstract class BaseArchiveVersion
    {
        protected readonly IFileSystem _fileSystem;
        protected readonly ILog _log;

        public ArchiveVersionInfo Info { get; set; }

        protected BaseArchiveVersion(ArchiveVersionInfo info, ILog log, IFileSystem fileSystem = null)
        {
            Info = info;            
            _log = log;
            _fileSystem = fileSystem ?? new FileSystem();
            Load(info.FolderPath);
        }
        /// <summary>
        /// Loads archive version indices to populate this object
        /// </summary>
        /// <param name="folderPath">the path to the ArchiveVersion's root folder</param>
        protected abstract void Load(string folderPath);
        /// <summary>
        /// Gets the file checksums in the archive versions index represented as a dictionary
        /// </summary>
        /// <returns>Key = file path, Value = MD5 checksum</returns>
        public abstract Dictionary<string, byte[]> GetChecksumDict();
        public Dictionary<string, bool> VerifyAllChecksums()
        {
            var files = _fileSystem.Directory.GetFiles(this.Info.FolderPath, "*", System.IO.SearchOption.AllDirectories);
            var result = new Dictionary<string, bool>();
            foreach (var file in files)
            {

                result.Add(file, VerifyChecksum(file));
            }
            return result;
        }
        public bool VerifyChecksum(string filePath)
        {
            var removeString = _fileSystem.Directory.GetParent(this.Info.FolderPath).FullName;
            var removeIndex = filePath.IndexOf(removeString);
            var relativePath = filePath.Remove(removeIndex, removeString.Length);
            var expected = this.GetChecksumDict().SingleOrDefault(x => x.Key.ToLower() == relativePath.ToLower()).Value;

            if (expected.Length == 0)
            {
                return false;
            }
            using (var md5 = MD5.Create())
            {
                using (var stream = _fileSystem.File.OpenRead(filePath))
                {
                    return expected == md5.ComputeHash(stream);
                }
            }
        }
    }
}
