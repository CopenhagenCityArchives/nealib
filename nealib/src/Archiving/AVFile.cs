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
        public AVFileType AvFileType { get; private set; }

        public AVFile(string path, string name, string indicatedSum)
        {
            FilePath = path;
            FileName = name;
            IndicatedChecksum = indicatedSum;
            
            SetFileType();

        }

        public bool ValidateIndicatedChecksum(string ArchiveversionBasePath)
        {
            if (IndicatedChecksum == null)
                throw new Exception("IndicatedChecksum is not set, cannot validate checksum");
            
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(Path.Combine(ArchiveversionBasePath, FilePath, FileName)))
                {
                    var hash = md5.ComputeHash(stream);
                    return IndicatedChecksum == BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                }
            }
        }

        private void SetFileType()
        {
            if (FileName.IndexOf("xsd") != -1) { this.AvFileType = AVFileType.SCHEMA; return; }

            if (FileName.IndexOf("fileIndex.xml") != -1) { this.AvFileType = AVFileType.FILEINDEX; return; }

            if (FileName.IndexOf("Index") != -1) { this.AvFileType = AVFileType.INDEX; return; }

            if (FileName.IndexOf("table") != -1) { this.AvFileType = AVFileType.TABLE; return; }

            if (FilePath.IndexOf("ContextDocumentation") != -1) { this.AvFileType = AVFileType.CONTEXTDOCUMENT; return; }

            if (FilePath.IndexOf("Documents") != -1) { this.AvFileType = AVFileType.DOCUMENT; return; }

            throw new AVFileTypeNotFoundException(FilePath + FileName);
        }
    }
}
