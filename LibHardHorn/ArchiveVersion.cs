using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Dynamic;
using HardHorn.Logging;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    /// <summary>
    /// An error from verifying the archive version.
    /// </summary>
    public class ArchiveVersionVerificationError
    {
        /// <summary>
        /// Possible types of verification errors.
        /// </summary>
        public enum ErrorType {
            TableNotKept,
            TableKeptInError,
            UnknownTable
        }

        /// <summary>
        /// The error type.
        /// </summary>
        public ErrorType Type { get; set; }

        /// <summary>
        /// The error message.
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// An archive version.
    /// </summary>
    public class ArchiveVersion
    {
        /// <summary>
        /// The tables of the archive version.
        /// </summary>
        public IEnumerable<Table> Tables { get; private set; }

        /// <summary>
        /// The columns of the archive version.
        /// </summary>
        public IEnumerable<Column> Columns
        {
            get
            {
                foreach (var table in Tables)
                    foreach (var column in table.Columns)
                        yield return column;
            }
        }

        string _id;
        /// <summary>
        /// The archive version id.
        /// </summary>
        public string Id { get { return _id; } }

        string _path;
        /// <summary>
        /// The path to the archive version.
        /// </summary>
        public string Path { get { return _path; } }

        /// <summary>
        /// Constructs an archive version.
        /// </summary>
        /// <param name="id">The archive version ID, eg. "AVID.ABC.1".</param>
        /// <param name="path">The path to the root of the archive version.</param>
        /// <param name="tables">The tables in the archive version.</param>
        public ArchiveVersion(string id, string path, IEnumerable<Table> tables)
        {
            Tables = tables;
            _id = id;
            _path = path;
        }

        /// <summary>
        /// Verify the archive version in relation to a URL, pointing to a JSON document representing the archive version.
        /// </summary>
        /// <param name="url">A URL to a JSON document.</param>
        /// <returns>The verification errors.</returns>
        public IEnumerable<ArchiveVersionVerificationError> VerifyURL(string url)
        {
            WebRequest request = WebRequest.CreateHttp(url);
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string json = string.Empty;

                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        json = reader.ReadToEnd();
                    }

                    foreach (var error in Verify(JObject.Parse(json)))
                    {
                        yield return error;
                    }
                }
            }
        }

        /// <summary>
        /// Verify the archive version in relation to a JSON representation of the archive version.
        /// </summary>
        /// <param name="json">A JSON representation of the </param>
        /// <returns>The verification errors.</returns>
        public IEnumerable<ArchiveVersionVerificationError> VerifyJSON(string json)
        {
            dynamic av = JsonConvert.DeserializeObject<ExpandoObject>(json);
            foreach (var error in Verify(av))
            {
                yield return error;
            }
        }

        /// <summary>
        /// Verify the archive version in relation to a dynamic object, that is the deserialized JSON object, representing the archive version.
        /// </summary>
        /// <param name="av">The deserialized JSON object.</param>
        /// <returns>The verification errors.</returns>
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
                            yield return new ArchiveVersionVerificationError() { Message = string.Format("{0} findes i {1}, men burde kasseres.", table.Name, Id), Type = ArchiveVersionVerificationError.ErrorType.TableKeptInError };
                        }
                        match = true;
                        break;
                    }
                }

                if (verifyTable.keep && !match)
                {
                    // Report error (Table missing from AV)
                    yield return new ArchiveVersionVerificationError() { Message = string.Format("{0} findes ikke i {1}.", verifyTable.name, Id), Type = ArchiveVersionVerificationError.ErrorType.TableNotKept };
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
                    yield return new ArchiveVersionVerificationError() { Message = string.Format("{0} findes i {1}, men er ukendt.", table.Name, Id), Type = ArchiveVersionVerificationError.ErrorType.UnknownTable };
 
                }
            }
        }

        /// <summary>
        /// Load an archive version from a path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static ArchiveVersion Load(string path, ILogger log, Action<Exception> callback = null)
        {
            var archiveVersion = new ArchiveVersion(System.IO.Path.GetFileName(path), path, null);
            archiveVersion.Tables = LoadTableIndex(archiveVersion, System.IO.Path.Combine(path, "Indices", "tableIndex.xml"), log, callback).ToList();
            return archiveVersion;
        }

        /// <summary>
        /// Load the tables from a table index XML file.
        /// </summary>
        /// <param name="archiveVersion">The archive version, the tables are a part of.</param>
        /// <param name="path">The path to the table index file.</param>
        /// <param name="log">The logger.</param>
        /// <returns>An enumerable of the tables.</returns>
        public static IEnumerable<Table> LoadTableIndex(ArchiveVersion archiveVersion, string path, ILogger log, Action<Exception> callback = null)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var tableIndexDocument = XDocument.Load(path);
            var xtables = tableIndexDocument.Descendants(xmlns + "tables").First();

            foreach (var xtable in xtables.Elements(xmlns + "table"))
            {
                Table table = Table.Parse(archiveVersion, xtable, log, callback);
                yield return table;
            }
        }

        /// <summary>
        /// Compare the tables of this archive version, with another set of tables.
        /// </summary>
        /// <param name="compareTables">The tables to compare with.</param>
        /// <returns>An enumerable of table comparisons.</returns>
        public IEnumerable<TableComparison> CompareWithTables(IEnumerable<Table> compareTables)
        {
            foreach (var table in Tables)
            {
                bool tableAdded = true;
                foreach (var oldTable in compareTables)
                {
                    if (table.Name.ToLower() == oldTable.Name.ToLower())
                    {
                        yield return table.CompareTo(oldTable); ;
                        tableAdded = false;
                        break;
                    }
                }

                if (tableAdded)
                {
                    var tableComparison = new TableComparison(table, null) { Name = table.Name, Added = true };
                    tableComparison.Columns.AddRange(table.Columns.Select(c =>
                    {
                        var col = new ColumnComparison(null, c) { Name = c.Name };
                        return col;
                    }));
                    yield return tableComparison;
                }
            }

            foreach (var oldTable in compareTables)
            {
                bool tableRemoved = true;
                foreach (var table in Tables)
                {
                    if (table.Name.ToLower() == oldTable.Name.ToLower())
                    {
                        tableRemoved = false;
                        break;
                    }
                }

                if (tableRemoved)
                {
                    var tableComparison = new TableComparison(null, oldTable) { Removed = true, Name = oldTable.Name };
                    tableComparison.Columns.AddRange(oldTable.Columns.Select(c =>
                    {
                        var col = new ColumnComparison(null, c) { Name = c.Name };
                        return col;
                    }));
                    yield return tableComparison;
                }
            }
        }
    }
}
