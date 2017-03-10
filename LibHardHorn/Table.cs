using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HardHorn.ArchiveVersion
{
    public class Table
    {
        public List<Column> Columns { get; private set; }

        public string Name { get; private set; }

        public string Folder { get; private set; }

        public int Rows { get; private set; }

        public Table(string name, string folder, int rows, List<Column> columns)
        {
            Name = name;
            Folder = folder;
            Columns = columns;
            Rows = rows;
        }

        public static bool TryParse(XNamespace ns, XElement xtable, out Table table)
        {
            string name = xtable.Element(ns + "name").Value;
            string folder = xtable.Element(ns + "folder").Value;
            int rows = int.Parse(xtable.Element(ns + "rows").Value);

            table = new Table(name, folder, rows, new List<Column>());

            var xcolumns = xtable.Element(ns + "columns");
            foreach (var xcolumn in xcolumns.Elements(ns + "column"))
            {
                Column column;
                if (Column.TryParse(ns, xcolumn, out column))
                {
                    (table.Columns as List<Column>).Add(column);
                }
                else
                {
                    table = null;
                    return false;
                }
            }

            return true;
        }
    }
}
