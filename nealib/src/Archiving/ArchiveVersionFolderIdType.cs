namespace NEA.Archiving
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
}
