using NEA.ArchiveModel;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NEA.Helpers
{
    public class MD5Helper
    {
        protected readonly IFileSystem _fileSystem;

        public MD5Helper(IFileSystem fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
        }
        public byte[] CalculateChecksum(string filepath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = _fileSystem.File.OpenRead(filepath))
                {
                    return md5.ComputeHash(stream);
                }
            }
        }
        public string CalculateChecksumString(string filepath)
        {
            return BitConverter.ToString(CalculateChecksum(filepath)).Replace("-", "");
        }
    }
}
