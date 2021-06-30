using System.Collections.Generic;

namespace NEA.ArchiveModel
{
    public enum AVRuleSet { BKG342, BKG1007, BKG128 }
    public class ArchiveVersionInfo
    {
        public AVRuleSet AvRuleSet { get; private set; }
        public string Id { get; private set; }
        public Dictionary<string,string> Medias { get; set; }
        public string FirstMediaPath { get { return Medias[Id + ".1"]; } }

        public ArchiveVersionInfo(string id, Dictionary<string, string> medias, AVRuleSet avRuleSet)
        {
            Medias = medias;
            Id = id;
            AvRuleSet = avRuleSet;
        }

        public ArchiveVersionInfo()
        {

        }

        
    }
}
