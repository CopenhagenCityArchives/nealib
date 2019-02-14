using HardHorn.Archiving;
using System.Collections.Generic;

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

    public class ForeignKeyComparison
    {
        public string Name { get; set; }

        public ForeignKey OldForeignKey { get; set; }
        public ForeignKey NewForeignKey { get; set; }
        public bool Added { get; set; }
        public bool Removed { get; set; }
        public bool Modified { get; set; }

        public List<ReferenceComparison> References { get; set; }
        public bool ReferencesModified { get; set; }

        public ForeignKeyComparison(ForeignKey newForeignKey, ForeignKey oldForeignKey)
        {
            OldForeignKey = oldForeignKey;
            NewForeignKey = newForeignKey;
            References = new List<ReferenceComparison>();

            Added = false;
            Removed = false;
            Modified = false;
            ReferencesModified = false;
        }
    }

    public class ReferenceComparison
    {
        public bool Added { get; set; }
        public bool Removed { get; set; }
        public bool Modified { get; set; }

        public Reference OldReference { get; set; }
        public Reference NewReference { get; set; }

        public bool ReferencedColumnModified { get; set; }

        public string ColumnName { get; set; }

        public ReferenceComparison(Reference newReference, Reference oldReference)
        {
            NewReference = newReference;
            OldReference = oldReference;
            ReferencedColumnModified = false;
            Added = false;
            Removed = false;
            Modified = false;
        }
    }

    public class TableComparison
    {
        public bool Added { get; set; }
        public List<ColumnComparison> Columns { get; set; }
        public List<ForeignKeyComparison> ForeignKeys { get; set; }
        public bool ColumnsModified { get; set; }
        public bool ForeignKeysModified { get; set; }
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
            ForeignKeys = new List<ForeignKeyComparison>();
            Added = false;
            Modified = false;
            Removed = false;
            DescriptionModified = false;
            ColumnsModified = false;
            ForeignKeysModified = false;
            RowsModified = false;
            FolderModified = false;
        }
    }
}
