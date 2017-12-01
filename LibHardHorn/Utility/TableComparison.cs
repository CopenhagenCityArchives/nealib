using System.Collections.Generic;

using HardHorn.Archiving;

namespace HardHorn.Utility
{
    public class TableComparison
    {
        public bool Added { get; set; }
        public List<ColumnComparison> Columns { get; set; }
        public bool ColumnsModified { get; set; }
        public bool DescriptionModified { get; set; }
        public bool FolderModified { get; set; }
        public bool Modified { get; set; }
        public string Name { get; set; }
        public Table NewTable { get; set; }
        public Table OldTable { get; set; }
        public bool Removed { get; set; }
        public bool RowsModified { get; set; }

        public TableComparison(Table newTable, Table oldTable)
        {
            NewTable = newTable;
            OldTable = oldTable;
            Columns = new List<ColumnComparison>();
            Added = false;
            Modified = false;
            Removed = false;
            DescriptionModified = false;
            ColumnsModified = false;
            RowsModified = false;
            FolderModified = false;
        }
    }
}
