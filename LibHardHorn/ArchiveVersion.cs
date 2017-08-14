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
        public static ArchiveVersion Load(string path, ILogger log)
        {
            var archiveVersion = new ArchiveVersion(System.IO.Path.GetFileName(path), path, null);
            archiveVersion.Tables = LoadTableIndex(archiveVersion, System.IO.Path.Combine(path, "Indices", "tableIndex.xml"), log).ToList();
            return archiveVersion;
        }

        public static IEnumerable<Table> LoadTableIndex(ArchiveVersion archiveVersion, string path, ILogger log)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var tableIndexDocument = XDocument.Load(path);
            var xtables = tableIndexDocument.Descendants(xmlns + "tables").First();

            foreach (var xtable in xtables.Elements(xmlns + "table"))
            {
                Table table = Table.Parse(archiveVersion, xtable, log);
                yield return table;
            }
        }

        public IEnumerable<dynamic> CompareWithTables(IEnumerable<Table> compareTables)
        {
            dynamic tableComparison, columnComparison;

            foreach (var table in Tables)
            {
                bool tableAdded = true;
                foreach (var oldTable in compareTables)
                {
                    if (table.Name.ToLower() == oldTable.Name.ToLower())
                    {
                        tableComparison = new ExpandoObject();
                        tableComparison.Name = table.Name;
                        tableComparison.NewTable = table;
                        tableComparison.OldTable = oldTable;
                        tableComparison.Columns = new List<dynamic>();
                        tableComparison.Added = false;
                        tableComparison.Modified = false;
                        tableComparison.Removed = false;
                        tableComparison.DescriptionModified = false;
                        tableComparison.ColumnsModified = false;
                        tableComparison.RowsModified = false;
                        tableComparison.FolderModified = false;

                        if (table.Rows != oldTable.Rows)
                        {
                            tableComparison.Modified = true;
                            tableComparison.RowsModified = true;
                        }

                        if (table.Description != oldTable.Description)
                        {
                            tableComparison.Modified = true;
                            tableComparison.DescriptionModified = true;
                        }

                        if (table.Folder != oldTable.Folder)
                        {
                            tableComparison.Modified = true;
                            tableComparison.FolderModified = true;
                        }

                        foreach (var column in table.Columns)
                        {
                            bool columnAdded = true;
                            foreach (var oldColumn in oldTable.Columns)
                            {
                                if (column.Name.ToLower() == oldColumn.Name.ToLower())
                                {
                                    columnComparison = new ExpandoObject();
                                    columnComparison.Name = oldColumn.Name;
                                    columnComparison.NewColumn = column;
                                    columnComparison.OldColumn = oldColumn;
                                    columnComparison.Added = false;
                                    columnComparison.Modified = false;
                                    columnComparison.Removed = false;
                                    columnComparison.DataTypeModified = false;
                                    columnComparison.NullableModified = false;
                                    columnComparison.DescriptionModified = false;
                                    columnComparison.IdModified = false;

                                    if (column.Description != oldColumn.Description)
                                    {
                                        columnComparison.Modified = true;
                                        columnComparison.DescriptionModified = true;
                                    }

                                    if (column.Type != oldColumn.Type)
                                    {
                                        columnComparison.Modified = true;
                                        columnComparison.DataTypeModified = true;
                                    }

                                    if (column.Param == null && oldColumn.Param != null)
                                    {
                                        columnComparison.Modified = true;
                                        columnComparison.DataTypeModified = true;
                                    }
                                    else if (column.Param != null && oldColumn.Param == null)
                                    {
                                        columnComparison.Modified = true;
                                        columnComparison.DataTypeModified = true;
                                    }
                                    else if (column.Param == null && oldColumn.Param == null)
                                    { }
                                    else if (column.Param.Length != oldColumn.Param.Length)
                                    {
                                        columnComparison.Modified = true;
                                        columnComparison.DataTypeModified = true;
                                    }
                                    else
                                    {
                                        for (int i = 0; i < column.Param.Length; i++)
                                        {
                                            if (column.Param[i] != oldColumn.Param[i])
                                            {
                                                columnComparison.Modified = true;
                                                columnComparison.DataTypeModified = true;
                                                break;
                                            }
                                        }
                                    }

                                    if (column.Nullable != oldColumn.Nullable)
                                    {
                                        columnComparison.Modified = true;
                                        columnComparison.NullableModified = true;
                                    }

                                    if (column.ColumnId != oldColumn.ColumnId)
                                    {
                                        columnComparison.Modified = true;
                                        columnComparison.IdModified = true;
                                    }

                                    if (columnComparison.Modified)
                                    {
                                        tableComparison.ColumnsModified = true;
                                        tableComparison.Modified = true;
                                    }

                                    tableComparison.Columns.Add(columnComparison);

                                    columnAdded = false;
                                    break;
                                }
                            }

                            if (columnAdded)
                            {
                                columnComparison = new ExpandoObject();
                                columnComparison.Name = column.Name;
                                columnComparison.NewColumn = column;
                                columnComparison.OldColumn = null;
                                columnComparison.Added = true;
                                columnComparison.Modified = false;
                                columnComparison.Removed = false;
                                columnComparison.DataTypeModified = false;
                                columnComparison.NullableModified = false;
                                columnComparison.DescriptionModified = false;
                                columnComparison.IdModified = false;
                                tableComparison.Columns.Add(columnComparison);
                            }
                        }

                        foreach (var oldColumn in oldTable.Columns)
                        {
                            bool columnRemoved = true;
                            foreach (var column in table.Columns)
                            {
                                if (column.Name.ToLower() == oldColumn.Name.ToLower())
                                {
                                    columnRemoved = false;
                                }
                            }

                            if (columnRemoved)
                            {
                                columnComparison = new ExpandoObject();
                                columnComparison.Name = oldColumn.Name;
                                columnComparison.NewColumn = null;
                                columnComparison.OldColumn = oldColumn;
                                columnComparison.Added = false;
                                columnComparison.Modified = false;
                                columnComparison.Removed = true;
                                columnComparison.DataTypeModified = false;
                                columnComparison.NullableModified = false;
                                columnComparison.DescriptionModified = false;
                                columnComparison.IdModified = false;
                                tableComparison.Columns.Add(columnComparison);
                            }
                        }

                        foreach (dynamic col in tableComparison.Columns)
                        {
                            tableComparison.Modified = col.Modified || tableComparison.Modified;
                            tableComparison.ColumnsModified = col.Modified || tableComparison.ColumnsModified;
                        }

                        yield return tableComparison;
                        tableAdded = false;
                        break;
                    }
                }

                if (tableAdded)
                {
                    tableComparison = new ExpandoObject();
                    tableComparison.Name = table.Name;
                    tableComparison.NewTable = table;
                    tableComparison.OldTable = null;
                    tableComparison.Columns = table.Columns.Select(c =>
                    {
                        dynamic col = new ExpandoObject();
                        col.Name = c.Name;
                        col.OldColumn = null;
                        col.NewColumn = c;
                        col.Added = false;
                        col.Removed = false;
                        col.Modified = false;
                        col.DataTypeModified = false;
                        col.NullableModified = false;
                        col.DescriptionModified = false;
                        col.IdModified = false;
                        return col;
                    });
                    tableComparison.Added = true;
                    tableComparison.Removed = false;
                    tableComparison.Modified = false;
                    tableComparison.ColumnsModified = false;
                    tableComparison.DescriptionModified = false;
                    tableComparison.RowsModified = false;
                    tableComparison.FolderModified = false;
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
                    tableComparison = new ExpandoObject();
                    tableComparison.Name = oldTable.Name;
                    tableComparison.NewTable = null;
                    tableComparison.OldTable = oldTable;
                    tableComparison.Columns = oldTable.Columns.Select(c =>
                    {
                        dynamic col = new ExpandoObject();
                        col.Name = c.Name;
                        col.OldColumn = c;
                        col.NewColumn = null;
                        col.Added = false;
                        col.Removed = false;
                        col.Modified = false;
                        col.DataTypeModified = false;
                        col.NullableModified = false;
                        col.DescriptionModified = false;
                        col.IdModified = false;
                        return col;
                    });
                    tableComparison.Added = false;
                    tableComparison.Removed = true;
                    tableComparison.Modified = false;
                    tableComparison.ColumnsModified = false;
                    tableComparison.DescriptionModified = false;
                    tableComparison.RowsModified = false;
                    tableComparison.FolderModified = false;
                    yield return tableComparison;
                }
            }
        }
    }
}
