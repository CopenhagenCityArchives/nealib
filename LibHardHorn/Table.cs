using HardHorn.Logging;
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

        public string Description { get; private set; }

        public Table(string name, string folder, int rows, string description, List<Column> columns)
        {
            Name = name;
            Folder = folder;
            Columns = columns;
            Rows = rows;
            Description = description;
        }

        public static Table Parse(XNamespace ns, XElement xtable, ILogger log)
        {
            string name = xtable.Element(ns + "name").Value;
            string folder = xtable.Element(ns + "folder").Value;
            int rows = int.Parse(xtable.Element(ns + "rows").Value);
            string desc = xtable.Element(ns + "description").Value;

            var table = new Table(name, folder, rows, desc, new List<Column>());

            int dummyCount = 1;
            var xcolumns = xtable.Element(ns + "columns");
            foreach (var xcolumn in xcolumns.Elements(ns + "column"))
            {
                try
                {
                    var column = Column.Parse(table, ns, xcolumn);
                    (table.Columns as List<Column>).Add(column);
                }
                catch (ArchiveVersionColumnTypeParsingException ex)
                {
                    log.Log(string.Format("En fejl opstod under afkodningen af kolonnen '{0}' i tabellen '{1}': Typen '{2}' er ikke valid.", ex.Name, table.Name, ex.Type), LogLevel.ERROR);
                    (table.Columns as List<Column>).Add(new Column(table, "DUMMY" + (dummyCount++).ToString(), DataType.NOT_DEFINED, false, null, "", ""));
                }
                catch (ArchiveVersionColumnParsingException ex)
                {
                    log.Log(string.Format("En fejl opstod under afkodningen af en kolonne i tabellen '{0}': {1}", table.Name, ex.Message), LogLevel.ERROR);
                    (table.Columns as List<Column>).Add(new Column(table, "DUMMY" + (dummyCount++).ToString(), DataType.NOT_DEFINED, false, null, "", ""));
                }
            }

            return table;
        }
    }
}
