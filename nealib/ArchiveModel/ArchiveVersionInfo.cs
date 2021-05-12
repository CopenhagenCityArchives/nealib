using System.Collections.Generic;

namespace NEA.ArchiveModel
{
    public enum AVRuleSet { BKG342, BKG1007, BKG128 }
    public class ArchiveVersionInfo
    {
        public AVRuleSet AvRuleSet { get; private set; }
        public string Id { get; private set; }
        public List<string> Medias { get; set; }

        public ArchiveVersionInfo(string id, AVRuleSet avRuleSet)
        {
            Id = id;
            AvRuleSet = avRuleSet;
        }

        public ArchiveVersionInfo()
        {

        }
        
    }
}
