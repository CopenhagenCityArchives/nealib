using HardHorn.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class Reference
    {
        public Column Column { get; private set; }
        public string ColumnName { get; private set; }
        public Column ReferencedColumn { get; private set; }
        public string ReferencedColumnName { get; private set; }

        public override string ToString()
        {
            return $"<ref: {ColumnName} -> {ReferencedColumnName}>";
        }

        public Reference(string columnName, string referencedColumnName)
        {
            ColumnName = columnName;
            ReferencedColumnName = referencedColumnName;
        }

        public void Initialize(ForeignKey foreignKey, NotificationCallback notify)
        {

            try
            {
                Column = foreignKey.Table.Columns.First(c => c.Name.ToLower() == ColumnName.ToLower());
                ReferencedColumn = foreignKey.ReferencedTable.Columns.First(c => c.Name.ToLower() == ReferencedColumnName.ToLower());

                if (Column.ParameterizedDataType.CompareTo(ReferencedColumn.ParameterizedDataType) != 0)
                {
                    notify?.Invoke(new ForeignKeyTypeErrorNotification(foreignKey));
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new Exception(string.Format("Kunne ikke oprette en reference fra {0}.{1} til {2}.{3} - feltet kunne ikke findes.", Column.Table.Name, Column.Name, foreignKey.ReferencedTable.Name, ReferencedColumnName), ex);
            }
        }

        public static Reference Parse(XElement element)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var columnName = element.Element(xmlns + "column").Value;
            var referencedColumnName = element.Element(xmlns + "referenced").Value;

            return new Reference(columnName, referencedColumnName);
        }

        public XElement ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "reference",
                new XElement(xmlns + "column", ColumnName),
                new XElement(xmlns + "referenced", ReferencedColumnName));
        }

        public ReferenceComparison CompareTo(Reference oldReference)
        {
            var referenceComparison = new ReferenceComparison(this, oldReference) { ColumnName = ColumnName };

            referenceComparison.ReferencedColumnModified = ReferencedColumnName.ToLower() != oldReference.ReferencedColumnName.ToLower();
            referenceComparison.Modified = referenceComparison.Modified || referenceComparison.ReferencedColumnModified;

            return referenceComparison;
        }
    }
}
