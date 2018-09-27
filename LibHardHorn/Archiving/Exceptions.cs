using System;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class ColumnParsingException : Exception
    {
        public XElement Element { get; set; }

        public Column Column { get; set; }
        public Table Table { get; set; }

        public ColumnParsingException(string message, XElement element, Table table) : base(message)
        {
            Element = element;
            Table = table;
        }
    }

    public class ColumnTypeParsingException : ColumnParsingException
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Id { get; set; }

        public ColumnTypeParsingException(string message, string type, XElement element, Column column, Table table) : base(message, element, table)
        {
            Column = column;
            Name = Column.Name;
            Type = type;
            Id = Column.ColumnId;
        }
    }

    public class ArchiveVersionXmlValidationException : Exception
    {
        public XElement Element { get; set; }

        public ArchiveVersionXmlValidationException(XElement element, string message) : base(message)
        {
            Element = element;
        }
    }

    public class ForeignKeyNotMatchingException : Exception
    {
        public ForeignKey ForeignKey { get; private set; }

        public ForeignKeyNotMatchingException(ForeignKey foreignKey)
        {
            ForeignKey = foreignKey;
        }
    }

    public class RequiredFieldMissingException : Exception
    {
        public string Field { get; private set; }
        public RequiredFieldMissingException(string field) : base(string.Format("Feltet '{0}' findes ikke.", field))
        {
            Field = field;
        }
    }

    public class ErrorFieldException : Exception
    {
        public string Field { get; private set; }
        public string Value { get; private set; }
        public ErrorFieldException(string field, string value) : base(string.Format("Feltet '{0}' er angivet forkert. Værdien '{1}' er ikke gyldig.", field, value))
        {
            Field = field;
            Value = value;
        }
    }
}
