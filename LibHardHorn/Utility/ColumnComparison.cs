using HardHorn.Archiving;

namespace HardHorn.Utility
{
    public class ColumnComparison
    {
        public Column NewColumn { get; set; }
        public Column OldColumn { get; set; }
        public bool Added { get; set; }
        public bool Modified { get; set; }
        public bool Removed { get; set; }
        public bool DataTypeModified { get; set; }
        public bool NullableModified { get; set; }
        public bool DescriptionModified { get; set; }
        public bool IdModified { get; set; }
        public string Name { get; internal set; }

        public ColumnComparison(Column newColumn, Column oldColumn)
        {
            NewColumn = newColumn;
            OldColumn = oldColumn;
            Added = false;
            Modified = false;
            Removed = false;
            DataTypeModified = false;
            NullableModified = false;
            DescriptionModified = false;
            IdModified = false;
        }
    }
}
