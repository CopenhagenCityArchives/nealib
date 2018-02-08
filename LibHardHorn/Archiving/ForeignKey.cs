using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class ForeignKey
    {
        public string Name { get; private set; }
        public Table Table { get; private set; }
        public List<Reference> References { get; private set; }
        public string ReferencedTableName { get; private set; }
        public Table ReferencedTable { get; private set; }

        public ForeignKey(string name, string referencedTableName, IEnumerable<Reference> references)
        {
            Name = name;
            ReferencedTableName = referencedTableName;
            References = new List<Reference>(references);
        }

        public void Initialize(TableIndex tableIndex, Table table)
        {
            Table = table;
            ReferencedTable = tableIndex.Tables.First(t => t.Name.ToLower() == ReferencedTableName.ToLower());
            foreach (var reference in References)
            {
                reference.Initialize(Table, ReferencedTable);
            }
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
                new XElement(xmlns + "referencedTable", ReferencedTable),
                References.Select(r => r.ToXml()));
        }
    }
}
