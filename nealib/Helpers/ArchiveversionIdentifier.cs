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
        private readonly string _folderPattern1007FirstMedia = @"^AVID\.[A-ZØÆÅ]{2,4}\.\d{1,5}.1$";
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
             * Archiveversion 1007 without base folder
             */
            if (Regex.IsMatch(curDir.Name, _folderPattern1007FirstMedia, RegexOptions.IgnoreCase))
            {
                var mainDir = _fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(curDir.FullName, "Indices"));
                if (mainDir.Exists)
                {
                    var id = curDir.Name.Substring(0, curDir.Name.Length - 2);
                    avInfo = new ArchiveVersionInfo(id, GetArciveversionMediaFolders(id,Directory.GetParent(curDir.FullName).FullName), AVRuleSet.BKG1007);
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
                    var id = "000" + curDir.Name.Substring(0, curDir.Name.Length - 3);
                    avInfo = new ArchiveVersionInfo(id, GetArciveversionMediaFolders(id, Directory.GetParent(curDir.FullName).FullName), AVRuleSet.BKG342);
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
        public List<ArchiveVersion> GetArchiveVersionFolders(string path)
        {
            var dirs = _fileSystem.DirectoryInfo.FromDirectoryName(path).GetDirectories();
            var archiveVersionDirectories = new List<string>();
            var avList = new List<ArchiveVersion>();

            foreach (var curDir in dirs)
            {
                // ArchiveVersionFolderIdType avFolder = new ArchiveVersionFolderIdType();
                if (TryGetAvFolder(out ArchiveVersionInfo avFolder, curDir.ToString()))
                {
                    avList.Add(ArchiveVersion.Create(avFolder, _fileSystem));
                }
            }
            avList.Sort((x, y) => string.Compare(x.Info.Id, y.Info.Id));
            return avList;
        }
        private Dictionary<string, string> GetArciveversionMediaFolders(string id, string folder)
        {
            Dictionary<string, string> medias = new Dictionary<string, string>();

            foreach (string potentialPath in _fileSystem.Directory.EnumerateDirectories(folder))
            {
                var directory = _fileSystem.DirectoryInfo.FromDirectoryName(potentialPath);

                if (Regex.IsMatch(directory.Name, _folderPattern1007FirstMedia) && potentialPath.Contains(id))
                {
                    var path = directory.FullName;
                    var mediaId = Path.GetFileName(path);
                    medias.Add(mediaId, path);
                }
            }

            return medias;
        }
    }
}
