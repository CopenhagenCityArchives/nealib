using System;
using System.Collections.Generic;
using System.Text;

namespace NEA.ArchiveModel
{
    public abstract class BaseArchiveVersion
    {
        public ArchiveVersionInfo Info { get; set; }

        protected BaseArchiveVersion(ArchiveVersionInfo info)
        {
            Info = info;
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
    }
}
