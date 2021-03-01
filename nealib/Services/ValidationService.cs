using log4net;
using NEA.ArchiveModel;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NEA.Services
{    
    public class ValidationService
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILog _log;

        public ValidationService(IFileSystem fileSystem, ILog log)
        {
            _fileSystem = fileSystem;
            _log = log;
        }
        public Dictionary<string,bool> VerifyAllChecksums(BaseArchiveVersion av)
        {
            var files = _fileSystem.Directory.GetFiles(av.Info.FolderPath, "*", System.IO.SearchOption.AllDirectories);
            var result = new Dictionary<string, bool>();
            foreach (var file in files)
            {
                
                result.Add(file, VerifyChecksum(av, file));
            }
            return result;
        }
        public bool VerifyChecksum(BaseArchiveVersion av, string filePath)
        {
            var removeString = _fileSystem.Directory.GetParent(av.Info.FolderPath).FullName;
            var removeIndex = filePath.IndexOf(removeString);
            var relativeFile = filePath.Remove(removeIndex, removeString.Length);
            var expected = av.GetChecksumDict().SingleOrDefault(x => x.Key.ToLower() == filePath.ToLower()).Value;

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
