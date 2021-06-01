using NEA.ArchiveModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;

namespace NEA.Helpers
{
    public class ArchiveVersionIdentifier
    {
        private readonly string _1007AVBase = @"^AVID\.[A-ZØÆÅ]{2,4}\.\d{1,5}$";
        private readonly string _1007AVMedia = @"^AVID\.[A-ZØÆÅ]{2,4}\.\d{1,5}.\d{1,3}$";
        private readonly string _342AVBase = @"^000\d{5}$";
        private readonly string _342AVMedia = @"^\d{5}00\d{1}$";

        private readonly IFileSystem _fileSystem;

        public ArchiveVersionIdentifier(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        /// <summary>
        /// Checks wether a given folder is an archive version
        /// </summary>
        /// <param name="avInfo">Output for arhive version information</param>
        /// <param name="path">File path of the folder to be checked</param>
        /// <returns></returns>
        private bool TryGetAvFolder(out ArchiveVersion av, string path)
        {
            var curDir = _fileSystem.DirectoryInfo.FromDirectoryName(path);
            ArchiveVersionInfo avInfo;

            /*
             * Archiveversion 1007 media folder match, ie. C:\AVID.KSA.1.1
             */
            if (Regex.IsMatch(curDir.Name, _1007AVMedia, RegexOptions.IgnoreCase))
            {
                var indicesDir = _fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(curDir.FullName, "Indices"));
                var documentsDir = _fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(curDir.FullName, "Documents"));
                if (indicesDir.Exists || documentsDir.Exists)
                {
                    avInfo = new ArchiveVersionInfo(curDir.Name.Substring(0, curDir.Name.Length - 2), AVRuleSet.BKG1007);
                    avInfo.Medias = GetArciveversionMediaFolders(avInfo, Directory.GetParent(curDir.FullName).ToString());
                    av = avInfo.GetArchiveVersion();
                    return true;
                }
            }
            /*
             * Archiveversion 342 media folder match, ie. C:\43123001
             */
            else if (Regex.IsMatch(curDir.Name, _342AVMedia, RegexOptions.IgnoreCase))
            {
                var mainFile = _fileSystem.FileInfo.FromFileName(curDir.FullName + "\\arkver.tab");
                if (mainFile.Exists)
                {
                    avInfo = new ArchiveVersionInfo("000" + curDir.Name.Substring(0, curDir.Name.Length - 3), AVRuleSet.BKG342);
                    avInfo.Medias = GetArciveversionMediaFolders(avInfo, Directory.GetParent(curDir.FullName).ToString());
                    av = avInfo.GetArchiveVersion();
                    return true;
                }
            }

            av = null;
            return false;
        }
        /// <summary>
        /// Returns the all archive versions found withing the given directory
        /// </summary>
        /// <param name="path">Directory path to check</param>
        /// <returns>A list of archive version information objects</returns>
        public List<ArchiveVersion> GetArchiveVersionInfosInFolder(string path)
        {
            List<ArchiveVersion> avFolderList = new List<ArchiveVersion>();

            // The current folder is an archiveversion
            if (TryGetAvFolder(out ArchiveVersion av1, path))
            {
                avFolderList.Add(av1);
                return avFolderList;
            }

            var dirs = _fileSystem.DirectoryInfo.FromDirectoryName(path).GetDirectories();

            foreach (var curDir in dirs)
            {
                if (TryGetAvFolder(out ArchiveVersion av2, curDir.ToString()))
                {
                    avFolderList.Add(av2);
                }
            }
            avFolderList.Sort((x, y) => string.Compare(x.Info.Id, y.Info.Id));
            return avFolderList;
        }

        public List<ArchiveVersion> GetArchiveVersionInfosInFolders(List<string> folders, int folderDepth, Dictionary<string, ArchiveVersion> avs)
        {
            if(avs == null)
            {
                avs = new Dictionary<string, ArchiveVersion>();
            }

            foreach (string folder in folders)
            {
                foreach (ArchiveVersion av in GetArchiveVersionInfosInFolder(folder))
                {
                    if (avs.ContainsKey(av.Info.Id))
                    {
                        avs[av.Info.Id].Info.Medias.AddRange(av.Info.Medias);
                    }
                    else
                    {
                        avs.Add(av.Info.Id, av);
                    }
                }

                if (folderDepth > 0)
                {
                    foreach (string subfolder in this._fileSystem.Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly))
                    {
                        GetArchiveVersionInfosInFolders(new List<string>() { subfolder }, folderDepth - 1, avs);
                    }
                }
            }
            List<ArchiveVersion> list = avs.Values.ToList();
            foreach (ArchiveVersion av in list)
            {
                av.Info.Medias.Sort((x, y) => string.Compare(x.MediaFolderName, y.MediaFolderName));
            }

            return list;
        }

        public List<ArchiveVersion> GetArchiveVersionInfosInFolders(List<string> folders)
        {
            return GetArchiveVersionInfosInFolders(folders, 0, null);
        }

        private List<ArchiveVersionMedia> GetArciveversionMediaFolders(ArchiveVersionInfo avInfo, string folderPath)
        {
            List<ArchiveVersionMedia> medias = new List<ArchiveVersionMedia>();
            foreach (string potentialPath in _fileSystem.Directory.EnumerateDirectories(folderPath))
            {
                var directory = _fileSystem.DirectoryInfo.FromDirectoryName(potentialPath);

                if (Regex.IsMatch(directory.Name, _1007AVMedia) && potentialPath.Contains(avInfo.Id))
                {
                    medias.Add(new ArchiveVersionMedia(potentialPath, directory.Name));
                }
            }

            return medias;
        }
    }
}
