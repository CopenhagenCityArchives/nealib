using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class ForeignKey
    {
        public class Reference
        {
            public string Column { get; private set; }
            public string Referenced { get; private set; }

            public Reference(string column, string referenced)
            {
                Column = column;
                Referenced = referenced;
            }

            public static Reference Parse(XElement element)
            {
                XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

                var column = element.Element(xmlns + "column").Value;
                var referenced = element.Element(xmlns + "referenced").Value;

                return new Reference(column, referenced);
            }

            public XElement ToXml()
            {
                XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

                return new XElement(xmlns + "reference",
                    new XElement(xmlns + "column", Column),
                    new XElement(xmlns + "referenced", Referenced));
            }
        }

        public string Name { get; private set; }
        public string ReferencedTable { get; private set; }
        public List<Reference> References { get; private set; }

        public ForeignKey(string name, string referencedTable, IEnumerable<Reference> references)
        {
            Name = name;
            ReferencedTable = referencedTable;
            References = new List<Reference>(references);
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
