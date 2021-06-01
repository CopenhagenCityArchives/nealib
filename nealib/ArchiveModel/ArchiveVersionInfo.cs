using System;
using System.Collections.Generic;

namespace NEA.ArchiveModel
{
    public enum AVRuleSet { BKG342, BKG1007, BKG128 }

    public class ArchiveVersionMedia
    {
        public string FullPath;
        public string MediaFolderName;

        public ArchiveVersionMedia(string FullPath, string MediaFolderName)
        {
            this.FullPath = FullPath;
            this.MediaFolderName = MediaFolderName;
        }

        public string GetParentPath()
        {
            return FullPath.Substring(FullPath.IndexOf(MediaFolderName));
        }
    }

    public class ArchiveVersionInfo
    {
        public AVRuleSet AvRuleSet { get; private set; }
        public string Id { get; private set; }
        public List<ArchiveVersionMedia> Medias { get; set; }

        public ArchiveVersionInfo(string id, AVRuleSet avRuleSet)
        {
            Id = id;
            AvRuleSet = avRuleSet;
        }

        public ArchiveVersionInfo()
        {

        }

        public ArchiveVersion GetArchiveVersion()
        {
            switch (AvRuleSet)
            {
                case AVRuleSet.BKG1007:
                    return new ArchiveVersion1007(this);
                default:
                    throw new NotImplementedException("The following ruleset is not implemented: " + AvRuleSet.ToString());
            }
        }

    }
}
