using System;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class ArchiveVersionColumnParsingException : Exception
    {
        public XElement Element { get; set; }

        public Column Column { get; set; }
        public Table Table { get; set; }

        public ArchiveVersionColumnParsingException(string message, XElement element, Table table) : base(message)
        {
            Element = element;
            Table = table;
        }
    }

    public class ArchiveVersionColumnTypeParsingException : ArchiveVersionColumnParsingException
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Id { get; set; }

        public ArchiveVersionColumnTypeParsingException(string message, string type, XElement element, Column column, Table table) : base(message, element, table)
        {
            Column = column;
            Name = Column.Name;
            Type = type;
            Id = Column.ColumnId;
        }
    }
}
