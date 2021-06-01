using log4net;
using System;
using System.Collections.Generic;
using System.IO;
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
        /// Gets this files path relative to the archive version
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="mediaId"></param>
        /// <returns></returns>
        public string GetRelativeFilePath(string filepath)
        {
            var mediaId = filepath.Split('\\').LastOrDefault(x => x.Contains(Info.Id));
            var removeString = _fileSystem.Directory.GetParent(Info.Medias[mediaId]).FullName;
            var removeIndex = filepath.IndexOf(removeString);
            return filepath.Remove(removeIndex, removeString.Length);
        }
        /// <summary>
        /// Gets a files absolute path based on one relative to the archive version
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public string GetAbsolutePath(string relativePath)
        {
            var mediaId = relativePath.Split('\\')[0];
            return _fileSystem.Directory.GetParent(Info.Medias[mediaId]).FullName.TrimEnd('\\') + "\\" + relativePath.TrimStart('\\');
        }
        public abstract TableReader GetTableReader(string tableName, string mediaId);

        public static ArchiveVersion Create(ArchiveVersionInfo info, IFileSystem fileSystem = null)
        {
            switch (info.AvRuleSet)
            {
                case AVRuleSet.BKG1007:
                    return new ArchiveVersion1007(info, fileSystem);
                case AVRuleSet.BKG342:                   
                case AVRuleSet.BKG128:
                default:
                    throw new ArgumentOutOfRangeException($"{info.Id} has unsuported AV ruleset: {info.AvRuleSet}");
            }
        }
    }

}
