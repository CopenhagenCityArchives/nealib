using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class PrimaryKey
    {
        public string Name { get; private set; }
        public IEnumerable<string> Columns { get; private set; }

        public PrimaryKey(string name, IEnumerable<string> columns)
        {
            Name = name;
            Columns = columns;
        }

        public static PrimaryKey Parse(XElement element)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var name = element.Element(xmlns + "name").Value;
            var columns = element.Elements(xmlns + "column").Select(e => e.Value);

            return new PrimaryKey(name, columns);
        }

        public XElement ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "primaryKey",
                new XElement(xmlns + "name", Name),
                Columns.Select(c => new XElement(xmlns + "column", c)));
        }
    }
}
