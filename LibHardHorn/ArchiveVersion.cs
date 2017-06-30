using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.ArchiveVersion
{
    public class ArchiveVersionVerificationError
    {
        public enum ErrorType {
            TableNotKept,
            TableKeptInError,
            UnknownTable
        }

        public ErrorType Type { get; set; }
        public string Message { get; set; }
    }

    public class ArchiveVersion
    {
        List<Table> _tables;
        public IEnumerable<Table> Tables { get { return _tables.Cast<Table>(); } }

        string _id;
        public string ID { get { return _id; } }

        string _path;
        public string Path { get { return _path; } }

        public ArchiveVersion(string id, string path, IEnumerable<Table> tables)
        {
            _tables = tables.ToList();
            _id = id;
            _path = path;
        }

        public IEnumerable<ArchiveVersionVerificationError> Verify(dynamic av)
        {
            foreach (dynamic verifyTable in av.tableIndex)
            {
                bool match = false;

                foreach (var table in Tables)
                {
                    if (table.Name.ToLower() == verifyTable.name.ToLower())
                    {
                        if (!verifyTable.keep)
                        {
                            yield return new ArchiveVersionVerificationError() { Message = string.Format("{0} findes i {1}, men burde kasseres.", table.Name, ID), Type = ArchiveVersionVerificationError.ErrorType.TableKeptInError };
                        }
                        match = true;
                        break;
                    }
                }

                if (verifyTable.keep && !match)
                {
                    // Report error (Table missing from AV)
                    yield return new ArchiveVersionVerificationError() { Message = string.Format("{0} findes ikke i {1}.", verifyTable.name, ID), Type = ArchiveVersionVerificationError.ErrorType.TableNotKept };
                }
            }

            foreach (var table in Tables)
            {
                bool match = false;
                 
                foreach (dynamic verifyTable in av.tableIndex)
                {
                    if (table.Name.ToLower() == verifyTable.name.ToLower())
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    yield return new ArchiveVersionVerificationError() { Message = string.Format("{0} findes i {1}, men er ukendt.", table.Name, ID), Type = ArchiveVersionVerificationError.ErrorType.UnknownTable };
 
                }
            }
        }
    }
}
