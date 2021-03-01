namespace NEA.ArchiveModel
{
    public enum AVRuleSet { BKG342, BKG1007, BKG128 }
    public class ArchiveVersionInfo
    {
        public AVRuleSet AvRuleSet { get; private set; }
        public string Id { get; private set; }
        public string FolderPath { get; private set; }

        public ArchiveVersionInfo(string id, string path, AVRuleSet avRuleSet)
        {
            FolderPath = path;
            Id = id;
            AvRuleSet = avRuleSet;
        }

        public ArchiveVersionInfo()
        {

        }
    }
}
