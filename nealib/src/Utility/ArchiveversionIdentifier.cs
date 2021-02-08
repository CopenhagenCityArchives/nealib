using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using NEA.Archiving;

namespace NEA.Utility
{
    public class ArchiveVersionIdentifier
    {
        private readonly string _folderPattern1007 = @"^AVID\.[A-ZØÆÅ]{2,4}\.\d{1,5}$";
        private readonly string _folderPattern1007NoBase = @"^AVID\.[A-ZØÆÅ]{2,4}\.\d{1,5}.1$";
        private readonly string _folderPattern342 = @"^000\d{5}$";
        private readonly string _folderPattern342NoBase = @"^\d{5}001$";

        private readonly IFileSystem _fileSystem;

        public ArchiveVersionIdentifier(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public bool TryGetAvFolder(out ArchiveVersionFolderIdType avFolderIdType, string path)
        {
            var curDir = _fileSystem.DirectoryInfo.FromDirectoryName(path);
            avFolderIdType = null;

            /*
             * Archiveversion 1007 with base folder
             */
            if (Regex.IsMatch(curDir.Name, _folderPattern1007, RegexOptions.IgnoreCase))
            {
                avFolderIdType = new ArchiveVersionFolderIdType(curDir.Name, curDir.FullName, AVRuleSet.BKG1007);
                return true;
            }
            /*
             * Archiveversion 342 with base folder
             */
            else if ((Regex.IsMatch(curDir.Name, _folderPattern342, RegexOptions.IgnoreCase)))
            {
                var mainFile = _fileSystem.FileInfo.FromFileName(Path.Combine(curDir.FullName, curDir.Name.Substring(3) + "001", "arkver.tab"));
                if (mainFile.Exists) { 
                    avFolderIdType = new ArchiveVersionFolderIdType(curDir.Name, curDir.FullName, AVRuleSet.BKG342);
                    return true;
                }
            }
            /*
             * Archiveversion 1007 without base folder
             */
            else if ((Regex.IsMatch(curDir.Name, _folderPattern1007NoBase, RegexOptions.IgnoreCase)))
            {
                var mainDir = _fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(curDir.FullName, "Indices"));
                if (mainDir.Exists) { 
                    avFolderIdType = new ArchiveVersionFolderIdType(curDir.Name.Substring(0, curDir.Name.Length - 2), curDir.FullName.Replace(curDir.Name, ""), AVRuleSet.BKG1007);
                    return true;
                }
            }
            /*
             * Archiveversion 342 without base folder
             */
            else if ((Regex.IsMatch(curDir.Name, _folderPattern342NoBase, RegexOptions.IgnoreCase)))
            {
                var mainFile = _fileSystem.FileInfo.FromFileName(curDir.FullName + "\\arkver.tab");
                if (mainFile.Exists) {
                    avFolderIdType = new ArchiveVersionFolderIdType("000" + curDir.Name.Substring(0, curDir.Name.Length - 3), curDir.FullName, AVRuleSet.BKG342);
                    return true;
                }
            }

            return false;
        }

        public List<ArchiveVersionFolderIdType> GetArchiveVersionFolders(string path)
        {
            var dirs = _fileSystem.DirectoryInfo.FromDirectoryName(path).GetDirectories();
            List<String> archiveVersionDirectories = new List<String>();
            List<ArchiveVersionFolderIdType> avFolderList = new List<ArchiveVersionFolderIdType>();

            foreach (var curDir in dirs)
            {
               // ArchiveVersionFolderIdType avFolder = new ArchiveVersionFolderIdType();
                if(TryGetAvFolder(out ArchiveVersionFolderIdType avFolder, curDir.ToString()))
                {
                    avFolderList.Add(avFolder);
                }
            }
            avFolderList.Sort((x, y) => string.Compare(x.Id, y.Id));
            return avFolderList;
        }
    }
}
