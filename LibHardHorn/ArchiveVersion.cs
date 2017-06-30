using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.ArchiveVersion
{
    class ArchiveVersion
    {
        List<Table> _tables = new List<Table>();
        public IEnumerable<Table> Tables { get { return _tables; } }

        string _id;
        public string ID { get { return _id; } }

        string _path;
        public string Path { get { return _path; } }

        public ArchiveVersion(string id, string path, IEnumerable<Table> tables)
        {
            _tables = tables.ToList<Table>();
            _id = id;
            _path = path;
        }

        public void Verify(dynamic av)
        {
            foreach (dynamic verifyTable in av.tableIndex)
            {
                var matchingTable = Tables.FirstOrDefault(t => verifyTable.name.ToLower() == t.Folder.ToLower());

                if (matchingTable == null)
                {
                    // TODO: Houston, we have an error!
                }
            }
        }
    }
}
