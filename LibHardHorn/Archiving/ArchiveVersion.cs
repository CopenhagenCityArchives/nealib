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
using System.Globalization;

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
            UnknownTable,
            Value,
            TableEmptiness
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
                foreach (var table in Tables) foreach (var column in table.Columns) yield return column;
            }
        }

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
        public DateTime? PeriodStart { get; private set; }

        /// <summary>
        /// Required end date of the data contained in the ArchiveVersion.
        /// </summary>
        public DateTime? PeriodEnd { get; private set; }

        /// <summary>
        /// Required indicator for whether the ArchiveVersion is a final submission.
        /// </summary>
        public bool PacketType { get; private set; }

        /// <summary>
        /// Required list of one or more authorities, which created the archive material.
        /// </summary>
        public IEnumerable<ArchiveCreator> ArchiveCreators { get; private set; }

        /// <summary>
        /// Required period type indicating whether this archive version is a completed
        /// period (true), or a snapshot (false).
        /// </summary>
        public bool PeriodType { get; private set; }

        /// <summary>
        /// Official name of the IT system, where all abbreviations have been expanded.
        /// </summary>
        public string SystemName { get; private set; }

        /// <summary>
        /// Optional list of alternative names of the IT system.
        /// </summary>
        public IEnumerable<string> AlternativeNames { get; private set; }

        /// <summary>
        /// Required description of the purpose of creating and running  the IT system.
        /// </summary>
        public string SystemPurpose { get; private set; }

        /// <summary>
        /// Required description of the main population and variables of the IT system, eg. what is registered in the system.
        /// </summary>
        public string SystemContent { get; private set; }

        /// <summary>
        /// Required indicator for whether the IT system systematically uses region numbers.
        /// </summary>
        public bool RegionNumbersUsed { get; private set; }

        /// <summary>
        /// Required indicator for whether the IT system systematically uses municipality (kommune) numbers.
        /// </summary>
        public bool KommuneNumbersUsed { get; private set; }

        /// <summary>
        /// Required indicator for whether the IT system systematically uses CPR numbers.
        /// </summary>
        public bool CPRNumbersUsed { get; private set; }

        /// <summary>
        /// Required indicator for whether the IT system systematically uses matrikel numbers.
        /// </summary>
        public bool MatrikelNumbersUsed { get; private set; }

        /// <summary>
        /// Required indicator for whether the IT system systematically uses CVR numbers.
        /// </summary>
        public bool CVRNumbersUsed { get; private set; }

        /// <summary>
        /// Required indicator for whether the IT system systematically uses BBR numbers.
        /// </summary>
        public bool BBRNumbersUsed { get; private set; }

        /// <summary>
        /// Required indicator for whether the IT system systematically uses WHO disease classification codes.
        /// </summary>
        public bool WHOCodesUsed { get; private set; }

        /// <summary>
        /// Optional list of other indices, which have contributed data to this IT system.
        /// </summary>
        public IEnumerable<string> SourceNames { get; private set; }

        /// <summary>
        /// Optional list of other IT systems, which have used data from this IT system.
        /// </summary>
        public IEnumerable<string> UserNames { get; private set; }

        /// <summary>
        /// Optional list of IT systems which have previously performed the functions of this IT system.
        /// </summary>
        public IEnumerable<string> PredecessorNames { get; private set; }

        /// <summary>
        /// Required (for public authorities) indication of the version of FORM the classifications have been collected from.
        /// </summary>
        public string FORMVersion { get; private set; }

        /// <summary>
        /// Required (for public authorities) list of FORM classifications.
        /// </summary>
        public IEnumerable<FORMClassification> FORMClassifications { get; private set; }

        /// <summary>
        /// Required indicator for whether the archive version contains digital documents apart from context documentation.
        /// </summary>
        public bool ContainsDigitalDocuments { get; private set; }

        /// <summary>
        /// Required indicator for whether the archive version is a necessary means of search for paper cases or documents, or cases and documents in another IT system.
        /// </summary>
        public bool SearchRelatedOtherRecords { get; private set; }

        /// <summary>
        /// Required (if SearchRelatedOtherRecords is true) reference to the archive materials, which the archive version is a search means of.
        /// </summary>
        public IEnumerable<string> RelatedRecordsNames { get; private set; }

        /// <summary>
        /// Required indicator for whether the IT system has a case concept, ie. connections between related documents are registered.
        /// </summary>
        public bool SystemFileConcept { get; private set; }

        /// <summary>
        /// Required indicator for whether the IT system is composed of data and documents from multiple IT systems in a service-oriented architecture.
        /// </summary>
        public bool MultipleDataCollection { get; private set; }

        /// <summary>
        /// Required indicator for whether the archive version contains sensitive personal information.
        /// </summary>
        public bool PersonalDataRestrictedInfo { get; private set; }

        /// <summary>
        /// Reqired indicator for whether the archive version contains information, which can be the cause of a longer availability threshold.
        /// </summary>
        public bool OtherAccessTypeRestrictions { get; private set; }

        /// <summary>
        /// Required identification of the public archive which approves the archive version.
        /// </summary>
        public string ArchiveApproval { get; private set; }

        /// <summary>
        /// Optional description of decisions regarding access to the material.
        /// </summary>
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

                    foreach (var error in VerifyJSON(json))
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
            dynamic archiveIndex = av.archiveIndex;

            if (archiveIndex.archiveInformationPackageID != Id)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "archiveInformationPackageID",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.archiveInformationPackageIDPrevious != PreviousId)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "archiveInformationPackageIDPrevious",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.systemName != SystemName)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "systemName",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.archiveType != PeriodType)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "archiveType",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.archiveInformationPacketType != PacketType)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "archiveInformationPacketType",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.regionNum != RegionNumbersUsed)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "regionNum",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.komNum != KommuneNumbersUsed)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "komNum",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.cprNum != CPRNumbersUsed)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "cprNum",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.cvrNum != CVRNumbersUsed)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "cvrNum",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.matrikNum != MatrikelNumbersUsed)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "matrikNum",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.bbrNum != BBRNumbersUsed)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "bbrNum",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.whoSygKod != WHOCodesUsed)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "whoSygKod",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.containsDigitalDocuments != ContainsDigitalDocuments)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "containsDigitalDocuments",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.searchRelatedOtherRecords != SearchRelatedOtherRecords)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "searchRelatedOtherRecords",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.systemFileConcept != SystemFileConcept)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "systemFileConcept",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.multipleDataCollection != MultipleDataCollection)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "multipleDataCollection",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.personalDataRestrictedInfo != PersonalDataRestrictedInfo)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "personalDataRestrictedInfo",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if (archiveIndex.otherAccessTypeRestrictions != OtherAccessTypeRestrictions)
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "otherAccessTypeRestrictions",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if ((archiveIndex.sourceName as IEnumerable<dynamic>).Any(sourceName => !SourceNames.Any(sname => sname == sourceName.datasource)))
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "sourceName",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if ((archiveIndex.predecessorName as IEnumerable<dynamic>).Any(predecessorName => !PredecessorNames.Any(predName => predName == predecessorName.predecessor)))
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "predecessorName",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            if ((archiveIndex.archiveCreators as IEnumerable<dynamic>).Any(archiveCreator => !ArchiveCreators.Any(aCreator =>
                {
                    return aCreator.Name == archiveCreator.creatorName
                    && aCreator.PeriodEnd.ToShortDateString() == archiveCreator.endDate
                    && aCreator.PeriodStart.ToShortDateString() == archiveCreator.startDate;
                })))
            {
                yield return new ArchiveVersionVerificationError
                {
                    Message = "predecessorName",
                    Type = ArchiveVersionVerificationError.ErrorType.Value
                };
            }

            foreach (dynamic verifyTable in av.tableIndex)
            {
                bool match = false;
                foreach (var table in Tables)
                {
                    if (table.Name.ToLower() == verifyTable.name.ToLower())
                    {
                        if ((verifyTable.empty && table.Rows != 0) || (!verifyTable.empty && table.Rows == 0))
                        {
                            yield return new ArchiveVersionVerificationError()
                            {
                                Message = string.Format("{0} har {1} rækker, men er sat til {2}", table.Name, table.Rows, verifyTable.empty ? "tom" : "ikke tom"),
                                Type = ArchiveVersionVerificationError.ErrorType.TableEmptiness
                            };
                        }

                        if (!verifyTable.keep)
                        {
                            yield return new ArchiveVersionVerificationError()
                            {
                                Message = string.Format("{0} findes i {1}, men burde kasseres.", table.Name, Id),
                                Type = ArchiveVersionVerificationError.ErrorType.TableKeptInError
                            };
                        }
                        match = true;
                        break;
                    }
                }

                if (verifyTable.keep && !match)
                {
                    // Report error (Table missing from AV)
                    yield return new ArchiveVersionVerificationError()
                    {
                        Message = string.Format("{0} findes ikke i {1}.", verifyTable.name, Id),
                        Type = ArchiveVersionVerificationError.ErrorType.TableNotKept
                    };
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
            archiveVersion.LoadArchiveIndex(System.IO.Path.Combine(path, "Indices", "archiveIndex.xml"), log);
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

            var previousIdElement = archiveIndex.Element(xmlns + "archiveInformationPackageIDPrevious");
            if (previousIdElement != null)
            {
                PreviousId = previousIdElement.Value;
            }
            else
            {
                PreviousId = null;
            }

            try
            {
                PeriodStart = DateTime.Parse(archiveIndex.Element(xmlns + "archivePeriodStart").Value);
            }
            catch (FormatException)
            {
                throw new ErrorFieldException("archivePeriodStart", archiveIndex.Element(xmlns + "archivePeriodStart").Value);
            }
            
            PeriodEnd = DateTime.Parse(archiveIndex.Element(xmlns + "archivePeriodEnd").Value);

            PacketType = bool.Parse(archiveIndex.Element(xmlns + "archiveInformationPacketType").Value);

            var creators = archiveIndex.Element(xmlns + "archiveCreatorList");
            var creatorNames = creators.Elements(xmlns + "creatorName").Select(el => el.Value).ToList();
            var creationPeriodStarts = creators.Elements(xmlns + "creationPeriodStart").Select(el => el.Value).ToList();
            var creationPeriodEnds = creators.Elements(xmlns + "creationPeriodEnd").Select(el => el.Value).ToList();
            var archiveCreators = new List<ArchiveCreator>();
            if (creatorNames.Count == creationPeriodStarts.Count && creationPeriodStarts.Count == creationPeriodEnds.Count)
            {
                for (int i = 0; i < creatorNames.Count; i++)
                {
                    DateTime creationPeriodStart, creationPeriodEnd;
                    if (!DateTime.TryParseExact(creationPeriodStarts[i], new string[] { "yyyy-MM-dd", "yyyy-MM", "yyyy" }, null, DateTimeStyles.None, out creationPeriodStart))
                    {
                        throw new ErrorFieldException("creationPeriodStart", creationPeriodStarts[i]);
                    }

                    if (!DateTime.TryParseExact(creationPeriodEnds[i], new string[] { "yyyy-MM-dd", "yyyy-MM", "yyyy" }, null, DateTimeStyles.None, out creationPeriodEnd))
                    {
                        throw new ErrorFieldException("creationPeriodEnd", creationPeriodEnds[i]);
                    }
                    
                    archiveCreators.Add(new ArchiveCreator(creatorNames[i], creationPeriodStart, creationPeriodEnd));
                }
            }
            else
            {
                throw new Exception("Antallet af arkivskabere og tilhørende perioder stemmer ikke.");
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
                throw new Exception("Antallet af FORM klassificeringer (formClass i index) og klassificeringstekster (formClassText i index) stemmer ikke.");
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

            var tableIndex = TableIndex.ParseFile(System.IO.Path.Combine(Path, "Indices", "tableIndex.xml"), log, callback);

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
