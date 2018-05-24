using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace HardHorn.Utility
{
    public class TableReplacer
    {
        public Table Table { get; set; }

        Stream _stream;
        XmlWriter _writer;
        Dictionary<int, ReplacementOperation> _operationMap = new Dictionary<int, ReplacementOperation>();

        string _xmlns;
        string _xsi;
        string _schemaLocation;

        public TableReplacer(Table table, IEnumerable<ReplacementOperation> operations, Stream stream)
        {
            Table = table;
            _xmlns = string.Format("http://www.sa.dk/xmlns/siard/1.0/schema0/{0}.xsd", Table.Folder);
            _xsi = "http://www.w3.org/2001/XMLSchema-instance";
            _schemaLocation = string.Format("http://www.sa.dk/xmlns/siard/1.0/schema0/{0}.xsd {0}.xsd", Table.Folder);

            foreach (var operation in operations)
            {
                var index = Table.Columns.IndexOf(operation.Column);
                if (index != -1)
                {
                    _operationMap.Add(index, operation);
                }
            }

            _stream = stream;
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  ";
            _writer = XmlWriter.Create(stream, settings);
        }

        public void WriteHeader()
        {

            _writer.WriteStartElement("table", _xmlns);
            _writer.WriteAttributeString("xmlns", "xsi", null, _xsi);
            _writer.WriteAttributeString("xsi", "schemaLocation", _xsi, _schemaLocation);
        }

        public void WriteFooter()
        {
            _writer.WriteEndElement();
        }

        public int Write(Post[,] posts, int rowCount)
        {
            int replaceCount = 0;

            for (int row = 0; row < rowCount; row++)
            {
                _writer.WriteStartElement("row");
                for (int col = 0; col < Table.Columns.Count(); col++)
                {
                    var tag = Table.Columns[col].ColumnId;
                    var post = posts[row, col];
                    if (_operationMap.ContainsKey(col))
                    {
                        var operation = _operationMap[col];
                        replaceCount += post.ReplacePattern(operation.Pattern, operation.Replacement);
                    }
                    _writer.WriteStartElement(tag);
                    _writer.WriteString(post.Data);
                    if (post.IsNull)
                    {
                        _writer.WriteAttributeString("xsi", "nil", null, "true");
                    }
                    _writer.WriteEndElement();
                }
                _writer.WriteEndElement();
            }

            return replaceCount;
        }

        public void Flush()
        {
            _writer.Flush();
        }
    }
}
