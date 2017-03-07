using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TableDataAnalyzer.Models
{
    class Table
    {
        List<Column> _columns;
        public List<Column> Columns { get { return _columns; } }

        string _name;
        public string Name { get { return _name; } }

        string _folder;
        public string Folder { get { return _folder; } }

        public Table(string name, string folder, List<Column> columns)
        {
            _name = name;
            _folder = folder;
            _columns = columns;
        }

        public static bool TryParse(XNamespace ns, XElement xtable, out Table table)
        {
            string name = xtable.Element(ns + "name").Value;
            string folder = xtable.Element(ns + "folder").Value;

            table = new Table(name, folder, new List<Column>());

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
