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
using HardHorn.Utility;

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

    public class ArchiveCreator
    {
        public string Name { get; private set; }
        public DateTime PeriodStart { get; private set; }
        public DateTime PeriodEnd { get; private set; }

        public ArchiveCreator(string name, DateTime periodStart, DateTime periodEnd)
        {
            Name = name;
            PeriodStart = periodStart;
            PeriodEnd = periodEnd;
        }
    }

    public class FORMClassification
    {
        public string Classification { get; private set; }
        public string Text { get; private set; }

        public FORMClassification(string classification, string text)
        {
            Classification = classification;
            Text = text;
        }
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
        public string Id { get; private set; }

        /// <summary>
        /// The path to the archive version.
        /// </summary>
        public string Path { get; private set; }

        public TableIndex TableIndex { get; private set; }

        /// <summary>
        /// Optional Id of previous submission of this system.
        /// </summary>
        public string PreviousId { get; private set; }

        /// <summary>
        /// Required beginning date of the data contained in the ArchiveVersion.
        /// </summary>
        public string PeriodStart { get; private set; }

        /// <summary>
        /// Required end date of the data contained in the ArchiveVersion.
        /// </summary>
        public string PeriodEnd { get; private set; }

        /// <summary>
        /// Required indicator for whether the ArchiveVersion is a final submission.
        /// </summary>
        public bool PacketType { get; private set; }

        public IEnumerable<ArchiveCreator> ArchiveCreators { get; private set; }

        public bool PeriodType { get; private set; }

        public string SystemName { get; private set; }
        public IEnumerable<string> AlternativeNames { get; private set; }
        public string SystemPurpose { get; private set; }
        public string SystemContent { get; private set; }
        public bool RegionNumbersUsed { get; private set; }
        public bool KommuneNumbersUsed { get; private set; }
        public bool CPRNumbersUsed { get; private set; }
        public bool MatrikelNumbersUsed { get; private set; }
        public bool CVRNumbersUsed { get; private set; }
        public bool BBRNumbersUsed { get; private set; }
        public bool WHOCodesUsed { get; private set; }
        public IEnumerable<string> SourceNames { get; private set; }
        public IEnumerable<string> UserNames { get; private set; }
        public IEnumerable<string> PredecessorNames { get; private set; }
        public string FORMVersion { get; private set; }
        public IEnumerable<FORMClassification> FORMClassifications { get; private set; }
        public bool ContainsDigitalDocuments { get; private set; }
        public bool SearchRelatedOtherRecords { get; private set; }
        public IEnumerable<string> RelatedRecordsNames { get; private set; }
        public bool SystemFileConcept { get; private set; }
        public bool MultipleDataCollection { get; private set; }
        public bool PersonalDataRestrictedInfo { get; private set; }
        public bool OtherAccessTypeRestrictions { get; private set; }
        public string ArchiveApproval { get; private set; }
        public string ArchiveRestrictions { get; private set; }

        /// <summary>
        /// Constructs an archive version.
        /// </summary>
        /// <param name="id">The archive version ID, eg. "AVID.ABC.1".</param>
        /// <param name="path">The path to the root of the archive version.</param>
        /// <param name="tables">The tables in the archive version.</param>
        public ArchiveVersion(string id, string path, IEnumerable<Table> tables)
        {
            Tables = tables;
            Id = id;
            Path = path;
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
        /// <param name="path">A path to the root of the archive versions first media.</param>
        /// <param name="log">A logger.</param>
        /// <returns></returns>
        public static ArchiveVersion Load(string path, ILogger log, Action<Exception> callback = null)
        {
            var archiveVersion = new ArchiveVersion(System.IO.Path.GetFileName(path), path, null);
            archiveVersion.LoadTableIndex(log, callback);
            return archiveVersion;
        }

        /// <summary>
        /// Load the given archive index file into this ArchiveVersion instance.
        /// </summary>
        /// <param name="path">The path to the archiveIndex.xml file.</param>
        /// <param name="log">A logger.</param>
        public void LoadArchiveIndex(string path, ILogger log)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var archiveIndexDocument = XDocument.Load(path);
            var archiveIndex = archiveIndexDocument.Element(xmlns + "archiveIndex");

            var previousIdElement = archiveIndex.Element(xmlns + "archiveInformationPackageID");
            if (previousIdElement != null)
            {
                PreviousId = previousIdElement.Value;
            }
            else
            {
                PreviousId = null;
            }

            PeriodStart = archiveIndex.Element(xmlns + "archivePeriodStart").Value;
            PeriodEnd = archiveIndex.Element(xmlns + "archivePeriodEnd").Value;
            PacketType = bool.Parse(archiveIndex.Element(xmlns + "archiveInformationPacketType").Value);

            var creatorNames = archiveIndex.Elements(xmlns + "creatorName").Select(el => el.Value).ToList();
            var creatorPeriodStarts = archiveIndex.Elements(xmlns + "creatorPeriodStart").Select(el => el.Value).ToList();
            var creatorPeriodEnds = archiveIndex.Elements(xmlns + "creatorPeriodEnd").Select(el => el.Value).ToList();
            var archiveCreators = new List<ArchiveCreator>();
            if (creatorNames.Count == creatorPeriodStarts.Count && creatorPeriodStarts.Count == creatorPeriodEnds.Count)
            {
                for (int i = 0; i < creatorNames.Count; i++)
                {
                    archiveCreators.Add(new ArchiveCreator(creatorNames[i], DateTime.Parse(creatorPeriodStarts[i]), DateTime.Parse(creatorPeriodEnds[i])));
                }
            }
            else
            {
                throw new Exception();
            }
            ArchiveCreators = archiveCreators;

            PeriodType = bool.Parse(archiveIndex.Element(xmlns + "archiveType").Value);

            SystemName = archiveIndex.Element(xmlns + "systemName").Value;
            AlternativeNames = archiveIndex.Elements(xmlns + "alternativeName").Select(el => el.Value);
            SystemPurpose = archiveIndex.Element(xmlns + "systemPurpose").Value;
            SystemContent = archiveIndex.Element(xmlns + "systemContent").Value;
            RegionNumbersUsed = bool.Parse(archiveIndex.Element(xmlns + "regionNum").Value);
            KommuneNumbersUsed = bool.Parse(archiveIndex.Element(xmlns + "komNum").Value);
            CPRNumbersUsed = bool.Parse(archiveIndex.Element(xmlns + "cprNum").Value);
            MatrikelNumbersUsed = bool.Parse(archiveIndex.Element(xmlns + "matrikNum").Value);
            CVRNumbersUsed = bool.Parse(archiveIndex.Element(xmlns + "cvrNum").Value);
            BBRNumbersUsed = bool.Parse(archiveIndex.Element(xmlns + "bbrNum").Value);
            WHOCodesUsed = bool.Parse(archiveIndex.Element(xmlns + "whoSygKod").Value);
            SourceNames = archiveIndex.Elements(xmlns + "sourceName").Select(el => el.Value);
            UserNames = archiveIndex.Elements(xmlns + "userName").Select(el => el.Value);
            PredecessorNames = archiveIndex.Elements(xmlns + "predecessorName").Select(el => el.Value);

            var form = archiveIndex.Element(xmlns + "form");
            FORMVersion = form.Element(xmlns + "formVersion").Value;
            var classList = form.Element(xmlns + "classList");
            var formClasses = classList.Elements(xmlns + "formClass").Select(el => el.Value).ToList();
            var formClassTexts = classList.Elements(xmlns + "formClassText").Select(el => el.Value).ToList();
            var formClassifications = new List<FORMClassification>();
            if (formClasses.Count == formClassTexts.Count)
            {
                for (int i = 0; i < formClasses.Count; i++)
                {
                    formClassifications.Add(new FORMClassification(formClasses[i], formClassTexts[i]));
                }
            }
            else
            {
                throw new Exception();
            }
            FORMClassifications = formClassifications;

            ContainsDigitalDocuments = bool.Parse(archiveIndex.Element(xmlns + "containsDigitalDocuments").Value);
            SearchRelatedOtherRecords = bool.Parse(archiveIndex.Element(xmlns + "searchRelatedOtherRecords").Value);
            RelatedRecordsNames = archiveIndex.Elements(xmlns + "relatedRecordsName").Select(el => el.Value);
            SystemFileConcept = bool.Parse(archiveIndex.Element(xmlns + "systemFileConcept").Value);
            MultipleDataCollection = bool.Parse(archiveIndex.Element(xmlns + "multipleDataCollection").Value);
            PersonalDataRestrictedInfo = bool.Parse(archiveIndex.Element(xmlns + "personalDataRestrictedInfo").Value);
            OtherAccessTypeRestrictions = bool.Parse(archiveIndex.Element(xmlns + "otherAccessTypeRestrictions").Value);
            ArchiveApproval = archiveIndex.Element(xmlns + "archiveApproval").Value;

            var archiveRestrictions = archiveIndex.Element(xmlns + "archiveRestrictions");
            if (archiveRestrictions != null)
            {
                ArchiveRestrictions = archiveRestrictions.Value;
            }
            else
            {
                ArchiveRestrictions = null;
            }
    }

        /// <summary>
        /// Load the tables from a table index XML file.
        /// </summary>
        /// <param name="archiveVersion">The archive version, the tables are a part of.</param>
        /// <param name="path">The path to the table index file.</param>
        /// <param name="log">The logger.</param>
        /// <returns>An enumerable of the tables.</returns>
        public void LoadTableIndex(ILogger log, Action<Exception> callback = null)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var tableIndex = Archiving.TableIndex.ParseFile(System.IO.Path.Combine(Path, "Indices", "tableIndex.xml"), log, callback);

            foreach (var table in tableIndex.Tables)
            {
                table.ArchiveVersion = this;
            }

            Tables = tableIndex.Tables;

            TableIndex = tableIndex;
        }

        public static IEnumerable<View> GetViews(string path)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var tableIndexDocument = XDocument.Load(path);
            var views = tableIndexDocument.Descendants(xmlns + "views");

            if (views == null)
                return Enumerable.Empty<View>();

            return views.Elements().Select(xview => View.Parse(xview));
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


        public enum TableSpecStatus
        {
            SPEC_MATCHING,
            SPEC_MISSING,
            SPEC_UNDEFINED
        }

        public IEnumerable<Tuple<TableSpecStatus, string>> CheckTableSpec(IEnumerable<string> specTableNames)
        {
            bool matched;

            foreach (var name in specTableNames)
            {
                if (name.Trim().Length > 0)
                {
                    matched = false;
                    foreach (var table in Tables)
                    {
                        if (table.Name.ToLower() == name.Trim().ToLower())
                        {
                            yield return new Tuple<TableSpecStatus, string>(TableSpecStatus.SPEC_MATCHING, table.Name);
                            matched = true;
                        }
                    }

                    if (!matched)
                    {
                        yield return new Tuple<TableSpecStatus, string>(TableSpecStatus.SPEC_MISSING, name);
                    }
                }
            }

            foreach (var table in Tables)
            {
                matched = false;
                foreach (var name in specTableNames)
                {
                    if (name.Trim().Length > 0)
                    {
                        if (table.Name.ToLower() == name.Trim().ToLower())
                        {
                            matched = true;
                        }
                    }
                }

                if (!matched)
                {
                    yield return new Tuple<TableSpecStatus, string>(TableSpecStatus.SPEC_UNDEFINED, table.Name);
                }
            }
        }
    }
}
