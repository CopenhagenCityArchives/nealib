using HardHorn.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class ForeignKey
    {
        public string Name { get; set; }
        public Table Table { get; private set; }
        public List<Reference> References { get; private set; }
        public string ReferencedTableName { get; private set; }
        public Table ReferencedTable { get; private set; }
        public string Columns { get { return string.Join("/", References.Select(r => r.ColumnName)); } }
        public string ReferencedColumns { get { return string.Join("/", References.Select(r => r.ReferencedColumnName)); } }


        public ForeignKey(string name, string referencedTableName, IEnumerable<Reference> references)
        {
            Name = name;
            ReferencedTableName = referencedTableName;
            References = new List<Reference>(references);
        }

        public bool Initialize(TableIndex tableIndex, Table table)
        {
            bool matchingDataTypes = true;
            Table = table;
            ReferencedTable = tableIndex.Tables.First(t => t.Name.ToLower() == ReferencedTableName.ToLower());
            foreach (var reference in References)
            {
                matchingDataTypes = matchingDataTypes && reference.Initialize(Table, ReferencedTable);
            }
            return matchingDataTypes;
        }

        public ForeignKeyValue GetValueFromRow(int row, Post[,] posts)
        {
            var values = new List<string>();
            foreach (var reference in References)
            {
                int index = reference.Column.ColumnIdNumber - 1;
                values.Add(posts[row, index].Data);
            }
            return new ForeignKeyValue(values.ToArray());
        }

        public ForeignKeyValue GetReferencedValueFromRow(int row, Post[,] posts)
        {
            var values = new List<string>();
            foreach (var reference in References)
            {
                int index = reference.ReferencedColumn.ColumnIdNumber - 1;
                values.Add(posts[row, index].Data);
            }
            return new ForeignKeyValue(values.ToArray());
        }

        public static ForeignKey Parse(XElement element)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var name = element.Element(xmlns + "name").Value;
            var referencedTable = element.Element(xmlns + "referencedTable").Value;
            var references = element.Elements(xmlns + "reference").Select(Reference.Parse);

            return new ForeignKey(name, referencedTable, references);
        }

        public XElement ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "foreignKey",
                new XElement(xmlns + "name", Name),
                new XElement(xmlns + "referencedTable", ReferencedTable.Name),
                References.Select(r => r.ToXml()));
        }

        public ForeignKeyComparison CompareTo(ForeignKey oldForeignKey)
        {
            var foreignKeyComparison = new ForeignKeyComparison(this, oldForeignKey);

            // Compare references
            foreach (var reference in References)
            {
                bool referenceAdded = true;
                foreach (var oldReference in oldForeignKey.References)
                {
                    if (reference.ColumnName.ToLower() == oldReference.ColumnName.ToLower())
                    {
                        var referenceComparison = reference.CompareTo(oldReference);
                        foreignKeyComparison.References.Add(referenceComparison);
                        referenceAdded = false;
                        break;
                    }
                }

                if (referenceAdded)
                {
                    foreignKeyComparison.References.Add(new ReferenceComparison(reference, null) { ColumnName = reference.ColumnName, Added = true });
                }
            }

            foreach (var oldReference in oldForeignKey.References)
            {
                bool referenceRemoved = true;
                foreach (var reference in References)
                {
                    if (reference.ColumnName.ToLower() == oldReference.ColumnName.ToLower())
                    {
                        referenceRemoved = false;
                    }
                }

                if (referenceRemoved)
                {
                    foreignKeyComparison.References.Add(new ReferenceComparison(null, oldReference) { ColumnName = oldReference.ColumnName, Removed = true });
                }
            }

            foreach (var reference in foreignKeyComparison.References)
            {
                foreignKeyComparison.Modified = reference.Modified || foreignKeyComparison.Modified;
                foreignKeyComparison.ReferencesModified = reference.Modified || foreignKeyComparison.ReferencesModified;
            }

            return foreignKeyComparison;
        }
    }
}
