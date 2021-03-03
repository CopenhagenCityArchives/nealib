using log4net;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NEA.ArchiveModel
{
    public abstract class ArchiveVersion
    {
        protected readonly IFileSystem _fileSystem;
        protected static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ArchiveVersionInfo Info { get; set; }

        protected ArchiveVersion(ArchiveVersionInfo info, IFileSystem fileSystem = null)
        {
            Info = info;
            _fileSystem = fileSystem ?? new FileSystem();
        }
        /// <summary>
        /// Loads all archive version indices to populate this object
        /// </summary>
        public abstract void Load();
        /// <summary>
        /// Gets the file checksums in the archive versions index represented as a dictionary
        /// </summary>
        /// <returns>Key = file path, Value = MD5 checksum</returns>
        public abstract Dictionary<string, byte[]> GetChecksumDict();
        /// <summary>
        /// Verify the checksums of all files within the archive versions against its manifest
        /// </summary>
        /// <param name="skipDocuments"></param>
        /// <returns></returns>
        public abstract VerifyChecksumsResult VerifyAllChecksums(bool skipDocuments = false);
        public class VerifyChecksumsResult
        {
            public int SkippedFiles { get; set; }
            public int CheckedFiles { get { return result.Count; } }
            public int FailedChecks { get { return result.Count(x => !x.Value); } }
            public Dictionary<string, bool> result { get; set; }

            public VerifyChecksumsResult(Dictionary<string, bool> result, int skippedFiles)
            {
                this.result = result;
                SkippedFiles = skippedFiles;
            }
        }
        /// <summary>
        /// Validates the MD5 checksum of a given file against the one saved in the archive versions manifest
        /// </summary>
        /// <param name="filePath">Full path of the file to be checked</param>
        /// <returns></returns>
        public abstract bool VerifyChecksum(string filePath);
        /// <summary>
        /// Gets this files path relative to the archive version
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public string GetRelativeFilePath(string filepath)
        {
            var removeString = _fileSystem.Directory.GetParent(this.Info.FolderPath).FullName;
            var removeIndex = filepath.IndexOf(removeString);
            return filepath.Remove(removeIndex, removeString.Length);
        }
        public abstract TableReader GetTableReader(string tableName);
    }
    
}
