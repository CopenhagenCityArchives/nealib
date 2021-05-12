using NEA.ArchiveModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Text.RegularExpressions;

namespace NEA.Helpers
{
    public class ArchiveVersionIdentifier
    {
        private readonly string _folderPattern1007 = @"^AVID\.[A-ZØÆÅ]{2,4}\.\d{1,5}$";
        private readonly string _folderPattern1007FirstMedia = @"^AVID\.[A-ZØÆÅ]{2,4}\.\d{1,5}.1$";
        private readonly string _folderPattern342 = @"^000\d{5}$";
        private readonly string _folderPattern342FirstMedia = @"^\d{5}001$";

        private readonly IFileSystem _fileSystem;

        public ArchiveVersionIdentifier(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public ArchiveVersionIdentifier()
        {
            _fileSystem = new FileSystem();
        }

        /// <summary>
        /// Checks wether a given folder is an archive version
        /// </summary>
        /// <param name="avInfo">Output for arhive version information</param>
        /// <param name="path">File path of the folder to be checked</param>
        /// <returns></returns>
        public bool TryGetAvFolder(out ArchiveVersionInfo avInfo, string path)
        {
            var curDir = _fileSystem.DirectoryInfo.FromDirectoryName(path);
            avInfo = null;

            /*
             * Archiveversion 1007 with base folder
             */
            if (Regex.IsMatch(curDir.Name, _folderPattern1007, RegexOptions.IgnoreCase))
            {
                avInfo = new ArchiveVersionInfo(curDir.Name, curDir.FullName, AVRuleSet.BKG1007);
                return true;
            }
            /*
             * Archiveversion 342 with base folder
             */
            else if (Regex.IsMatch(curDir.Name, _folderPattern342, RegexOptions.IgnoreCase))
            {
                var mainFile = _fileSystem.FileInfo.FromFileName(Path.Combine(curDir.FullName, curDir.Name.Substring(3) + "001", "arkver.tab"));
                if (mainFile.Exists)
                {
                    avInfo = new ArchiveVersionInfo(curDir.Name, curDir.FullName, AVRuleSet.BKG342);
                    return true;
                }
            }
            /*
             * Archiveversion 1007 without base folder
             */
            else if (Regex.IsMatch(curDir.Name, _folderPattern1007FirstMedia, RegexOptions.IgnoreCase))
            {
                var mainDir = _fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(curDir.FullName, "Indices"));
                if (mainDir.Exists)
                {
                    avInfo = new ArchiveVersionInfo(curDir.Name.Substring(0, curDir.Name.Length - 2), Directory.GetParent(curDir.FullName).FullName, AVRuleSet.BKG1007);
                    avInfo.Medias = GetArciveversionMediaFolders(avInfo);
                    return true;
                }
            }
            /*
             * Archiveversion 342 without base folder
             */
            else if (Regex.IsMatch(curDir.Name, _folderPattern342FirstMedia, RegexOptions.IgnoreCase))
            {
                var mainFile = _fileSystem.FileInfo.FromFileName(curDir.FullName + "\\arkver.tab");
                if (mainFile.Exists)
                {
                    avInfo = new ArchiveVersionInfo("000" + curDir.Name.Substring(0, curDir.Name.Length - 3), Directory.GetParent(curDir.FullName).FullName, AVRuleSet.BKG342);
                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Returns the all archive versions found withing the given directory
        /// </summary>
        /// <param name="path">Directory path to check</param>
        /// <returns>A list of archive version information objects</returns>
        public List<ArchiveVersionInfo> GetArchiveVersionFolders(string path)
        {
            var dirs = _fileSystem.DirectoryInfo.FromDirectoryName(path).GetDirectories();
            List<string> archiveVersionDirectories = new List<string>();
            List<ArchiveVersionInfo> avFolderList = new List<ArchiveVersionInfo>();

            // The current folder is an archiveversion
            if (TryGetAvFolder(out ArchiveVersionInfo avInfo, path))
            {
                avFolderList.Add(avInfo);
                return avFolderList;
            }

            foreach (var curDir in dirs)
            {
                // ArchiveVersionFolderIdType avFolder = new ArchiveVersionFolderIdType();
                if (TryGetAvFolder(out ArchiveVersionInfo avFolder, curDir.ToString()))
                {
                    avFolderList.Add(avFolder);
                }
            }
            avFolderList.Sort((x, y) => string.Compare(x.Id, y.Id));
            return avFolderList;
        }
        private List<string> GetArciveversionMediaFolders(ArchiveVersionInfo avInfo)
        {
            List<string> medias = new List<string>();
            string folderToCheck = avInfo.FolderPath;

            foreach (string potentialPath in _fileSystem.Directory.EnumerateDirectories(folderToCheck))
            {
                var directory = _fileSystem.DirectoryInfo.FromDirectoryName(potentialPath);

                if (Regex.IsMatch(directory.Name, _folderPattern1007FirstMedia) && potentialPath.Contains(avInfo.Id))
                {
                    medias.Add(directory.FullName);
                }
            }

            return medias;
        }
    }
}
