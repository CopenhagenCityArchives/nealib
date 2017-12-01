using System;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class View
    {
        public string Name { get; private set; }
        public string QueryOriginal { get; private set; }
        public string Description { get; private set; }

        public View(string name, string queryOriginal, string description)
        {
            Name = name;
            QueryOriginal = queryOriginal;
            Description = description;
        }

        public static View Parse(XElement element)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var name = element.Element(xmlns + "name").Value;
            var queryOriginal = element.Element(xmlns + "queryOriginal").Value;
            string description = null;
            try
            {
                description = element.Element(xmlns + "description").Value;
            }
            catch (NullReferenceException) { }

            return new View(name, queryOriginal, description);
        }

        public XElement ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "view",
                new XElement(xmlns + "name", Name),
                new XElement(xmlns + "queryOriginal", QueryOriginal),
                Description == null ? null : new XElement(xmlns + "description", Description));
        }
    }
}
