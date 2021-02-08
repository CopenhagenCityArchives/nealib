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
        private string _filePath;
        private string _fileName;
        private string _checksum;
        private string _absolutePath;
        private ArchiveVersion _archiveversion;
        private AVFileType _avFileType;

        public AVFile(string path, string name, ArchiveVersion archiveversion, string checksum = null)
        {
            _filePath = path;
            _fileName = name;
            _archiveversion = archiveversion;
            _checksum = checksum;
        }

        public bool ValidateChecksum(string checksumIn)
        {
            if(_checksum == null)
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(this.GetAbsolutePath()))
                    {
                        _checksum = md5.ComputeHash(stream).ToString();
                    }
                }
            }

            return _checksum == checksumIn;
        }

        public string GetAbsolutePath()
        { 
            if(_absolutePath == null)
            {
                _absolutePath = Path.Combine(_archiveversion.Path, _filePath, _fileName);
            }
            return _absolutePath;
        }

        public AVFileType GetFileType()
        {
            if(_avFileType.Equals(null)) { return _avFileType; }
            
            if (_fileName.IndexOf("xsd") != -1) { _avFileType = AVFileType.SCHEMA; return _avFileType; }

            if (_fileName.IndexOf("fileIndex.xml") != -1) { _avFileType = AVFileType.FILEINDEX; return _avFileType; }

            if (_fileName.IndexOf("Index") != -1) { _avFileType = AVFileType.INDEX; return _avFileType; }

            if (_fileName.IndexOf("table") != -1) { _avFileType = AVFileType.TABLE; return _avFileType; }

            if (_filePath.IndexOf("Context") != -1) { _avFileType = AVFileType.CONTEXTDOCUMENT; return _avFileType; }

            if (_filePath.IndexOf("Documents") != -1) { _avFileType = AVFileType.DOCUMENT; return _avFileType; }

            throw new AVFileTypeNotFoundException(this.GetAbsolutePath());
        }
    }
}
