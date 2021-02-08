using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace NEA.Utility
{
    public enum AVRuleSet { BKG342, BKG1007, BKG128 }

    public class ArchiveVersionFolderIdType
    {
        private string _baseFolder;
        private string _id;
        private AVRuleSet _avRuleSet;

        public string Id { get { return _id; } }

        public ArchiveVersionFolderIdType(string id, string folder, AVRuleSet avRuleSet)
        {
            _baseFolder = folder;
            _id = id;
            _avRuleSet = avRuleSet;
        }
    }

    public class ArchiveVersionIdentifier
    {
        private readonly string _folderPattern1007 = @"^AVID\.[A-ZØÆÅ]{2,4}\.\d{1,5}$";
        private readonly string _folderPattern1007NoBase = @"^AVID\.[A-ZØÆÅ]{2,4}\.\d{1,5}.1$";
        private readonly string _folderPattern342 = @"^000\d{5}$";
        private readonly string _folderPattern342NoBase = @"^\d{5}001$";

        private DirectoryInfo dir;
        private List<string> existingArchiveVersions;

        public bool GetAVFromPath(out ArchiveVersionFolderIdType avFolderIdType, string path)
        {
            DirectoryInfo curDir = new DirectoryInfo(path);
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
                FileInfo mainDir = new FileInfo(Path.Combine(curDir.FullName, curDir.Name.Substring(3) + "001", "arkver.tab"));
                if (mainDir.Exists) { 
                    avFolderIdType = new ArchiveVersionFolderIdType(curDir.Name, curDir.FullName, AVRuleSet.BKG342);
                    return true;
                }
            }
            /*
             * Archiveversion 1007 without base folder
             */
            else if ((Regex.IsMatch(curDir.Name, _folderPattern1007NoBase, RegexOptions.IgnoreCase)))
            {
                DirectoryInfo mainDir = new DirectoryInfo(Path.Combine(curDir.FullName, "Indices"));
                if (mainDir.Exists) { 
                    //CurDir.Name = d:\AVID.GBS.3.1
                    avFolderIdType = new ArchiveVersionFolderIdType(curDir.Name.Substring(0, curDir.Name.Length - 2), curDir.FullName.Replace(curDir.Name, ""), AVRuleSet.BKG1007);
                    return true;
                }
            }
            /*
             * Archiveversion 342 without base folder
             */
            else if ((Regex.IsMatch(curDir.Name, _folderPattern342NoBase, RegexOptions.IgnoreCase)))
            {
                FileInfo mainFile = new FileInfo(curDir.FullName + "\\arkver.tab");
                if (mainFile.Exists) { 
                    //CurDir.Name = d:\10084001
                    avFolderIdType = new ArchiveVersionFolderIdType("000" + curDir.Name.Substring(0, curDir.Name.Length - 3), curDir.FullName.Replace(curDir.Name, ""), AVRuleSet.BKG342);
                    return true;
                }
            }

            return false;
        }

        public List<ArchiveVersionFolderIdType> getArchiveVersionFolders()
        {
            DirectoryInfo[] dirs;
            dirs = this.dir.GetDirectories();
            List<String> archiveVersionDirectories = new List<String>();
            List<ArchiveVersionFolderIdType> avFolderList = new List<ArchiveVersionFolderIdType>();

            foreach (DirectoryInfo curDir in dirs)
            {
               // ArchiveVersionFolderIdType avFolder = new ArchiveVersionFolderIdType();
                if(GetAVFromPath(out ArchiveVersionFolderIdType avFolder, curDir.ToString()))
                {
                    avFolderList.Add(avFolder);
                }
            }
            avFolderList.Sort((x, y) => string.Compare(x.Id, y.Id));
            return avFolderList;
        }
    }
}
