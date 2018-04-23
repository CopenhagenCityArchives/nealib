using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Utility
{
    public class TableReplacer
    {
        public Table Table { get; set; }

        Stream _stream;
        TextWriter _writer;
        Dictionary<int, ReplacementOperation> _operationMap = new Dictionary<int, ReplacementOperation>();

        public TableReplacer(Table table, IEnumerable<ReplacementOperation> operations, Stream stream)
        {
            Table = table;

            foreach (var operation in operations)
            {
                var index = Table.Columns.IndexOf(operation.Column);
                if (index != -1)
                {
                    _operationMap.Add(index, operation);
                }
            }

            _stream = stream;
            _writer = new StreamWriter(stream);
        }

        public void WriteHeader()
        {
            _writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            _writer.WriteLine(string.Format("<table xmlns=\"http://www.sa.dk/xmlns/siard/1.0/schema0/{0}.xsd\" "
                + "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" "
                + "xsi:schemaLocation=\"http://www.sa.dk/xmlns/siard/1.0/schema0/{0}.xsd table{0}.xsd\">", Table.Folder));
        }

        public void WriteFooter()
        {
            _writer.WriteLine("</table>");
        }

        public void Write(Post[,] posts, int rowCount)
        {
            for (int row = 0; row < rowCount; row++)
            {
                _writer.WriteLine("  <row>");
                for (int col = 0; col < Table.Columns.Count(); col++)
                {
                    var tag = Table.Columns[col].ColumnId;
                    var post = posts[row, col];
                    if (_operationMap.ContainsKey(col))
                    {
                        var operation = _operationMap[col];
                        post = post.ReplacePattern(operation.Pattern, operation.Replacement);
                    }
                    _writer.WriteLine(string.Format("    <{0}{2}>{1}</{0}>", tag, post.Data, post.IsNull ? " xsi:nil=\"true\"" : ""));
                }
                _writer.WriteLine("  </row>");
            }
        }

        public void Flush()
        {
            _writer.Flush();
        }
    }
}
