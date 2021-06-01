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
        /// Gets and object containing lists of Archive Versions various file types
        /// </summary>
        public abstract GetFilesResult GetFiles();
        public class GetFilesResult
        {
            /// <summary>
            /// A collection of paths for the Archive Versions metadata files such as indices, table definitions and context documents
            /// </summary>
            public string[] MetadataData { get; set; }
            /// <summary>
            /// A collection of paths for the Archive Versions files containing table row data
            /// </summary>
            public string[] TableData { get; set; }
            /// <summary>
            /// A collection of paths for the Archive Versions document files
            /// </summary>
            public string[] Documents { get; set; }
            /// <summary>
            /// Returns a concatenation of all file collections
            /// </summary>
            /// <returns>A collection of all files found within the Archive Version</returns>
            public IEnumerable<string> GetAll ()
            {
                return MetadataData.Concat(TableData).Concat(Documents);
            }
            public GetFilesResult(string[] metadataData, string[] tableData, string[] documents)
            {
                MetadataData = metadataData;
                TableData = tableData;
                Documents = documents;
            }
        }
        /// <summary>
        /// Gets this files path relative to the archive version
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public string GetRelativeFilePath(string filepath)
        {
            var removeString = _fileSystem.Directory.GetParent(this.Info.Medias[0].FullPath).FullName;
            var removeIndex = filepath.IndexOf(removeString);
            return filepath.Remove(removeIndex, removeString.Length+1);
        }
        public abstract TableReader GetTableReader(string tableName);
    }
    
}
