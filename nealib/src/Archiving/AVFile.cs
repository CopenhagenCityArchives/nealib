using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NEA.Archiving
{
    public enum AVFileType 
    {
        TABLE,
        INDEX,
        SCHEMA,
        DOCUMENT,
        CONTEXTDOCUMENT,
        FILEINDEX
    };

    public class AVFile
    {
        public string FilePath { get; private set; }
        public string FileName { get; private set; }
        public string IndicatedChecksum { get; private set; }
        public string AbsolutePath { get; private set; }
        public ArchiveVersion Archiveversion { get; private set; }
        public AVFileType AvFileType { get; private set; }

        public AVFile(string path, string name, ArchiveVersion archiveversion, string indicatedSum)
        {
            FilePath = path;
            FileName = name;
            Archiveversion = archiveversion;
            IndicatedChecksum = indicatedSum;
        }

        public bool ValidateIndicatedChecksum()
        {
            if (IndicatedChecksum == null)
                throw new Exception("IndicatedChecksum is not set, cannot validate checksum");
            
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(this.GetAbsolutePath()))
                {
                    var hash = md5.ComputeHash(stream);
                    return IndicatedChecksum == BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                }
            }
        }

        public string GetAbsolutePath()
        { 
            if(AbsolutePath == null)
            {
                AbsolutePath = Path.Combine(Archiveversion.Path, FilePath, FileName);
            }
            return AbsolutePath;
        }

        public AVFileType GetFileType()
        {
            if(AvFileType.Equals(null)) { return AvFileType; }
            
            if (FileName.IndexOf("xsd") != -1) { AvFileType = AVFileType.SCHEMA; return AvFileType; }

            if (FileName.IndexOf("fileIndex.xml") != -1) { AvFileType = AVFileType.FILEINDEX; return AvFileType; }

            if (FileName.IndexOf("Index") != -1) { AvFileType = AVFileType.INDEX; return AvFileType; }

            if (FileName.IndexOf("table") != -1) { AvFileType = AVFileType.TABLE; return AvFileType; }

            if (FilePath.IndexOf("Context") != -1) { AvFileType = AVFileType.CONTEXTDOCUMENT; return AvFileType; }

            if (FilePath.IndexOf("Documents") != -1) { AvFileType = AVFileType.DOCUMENT; return AvFileType; }

            throw new AVFileTypeNotFoundException(this.GetAbsolutePath());
        }
    }
}
