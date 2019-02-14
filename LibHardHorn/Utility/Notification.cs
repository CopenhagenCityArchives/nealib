using HardHorn.Analysis;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Data;
using System.Xml.Linq;

namespace HardHorn.Utility
{
    public enum Severity
    {
        Hint,
        Error
    }

    public enum NotificationType
    {
        XmlError,
        ColumnTypeError,
        TableRowCountError,
        ForeignKeyTypeError,
        ColumnParsing,
        DataTypeSuggestion,
        ForeignKeyTestError,
        ForeignKeyTestBlank,
        AnalysisErrorBlank,
        AnalysisErrorFormat,
        AnalysisErrorOverflow,
        AnalysisErrorRegex,
        AnalysisErrorUnderflow,
        ParameterSuggestion,
        DataTypeIllegalAlias,
        Suggestion,
        AnalysisErrorRepeatingChar,
        AnalysisErrorUnallowedKeyword,
        ForeignKeyReferencedTableMissing
    }

    public delegate void NotificationCallback(INotification notification);


    public interface INotification
    {
        NotificationType Type { get; }
        Severity Severity { get; }
        Column Column { get; }
        Table Table { get; }
        string Message { get; }
        int? Count { get; }
    }

    public class DataTypeIllegalAliasNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.DataTypeIllegalAlias; } }
        public Severity Severity { get { return Severity.Hint; } }
        public Column Column { get; private set; }
        public Table Table { get { return Column.Table; } }
        public string Message { get { return $"DataTypen '{DataTypeValue}' er et ulovligt alias for '{DataTypeUtility.ToString(DataType)}'."; } }
        public int? Count { get { return null; } }
        public AnalysisTestType TestType { get; private set; }
        public string DataTypeValue { get; private set; }
        public DataType DataType { get; private set; }

        public DataTypeIllegalAliasNotification(Column column, string dataTypeValue, DataType dataType)
        {
            Column = column;
            DataTypeValue = dataTypeValue;
            DataType = dataType;
        }
    }


    public class AnalysisErrorNotification : INotification
    {
        public NotificationType Type { get; private set; }
        public Severity Severity { get; private set; }
        public Column Column { get; private set; }
        public Table Table { get { return Column.Table; } }
        public string Message { get; private set; }
        public int? Count { get; set; }
        public AnalysisTestType TestType { get; private set; }
        public Post Post { get; private set; }

        public AnalysisErrorNotification(Test test, Column column, Post post)
        {
            TestType = test.Type;
            Severity = Severity.Error;
            switch (TestType)
            {
                case AnalysisTestType.BLANK:
                    Type = NotificationType.AnalysisErrorBlank;
                    Message = "Der findes blanktegn i starten eller slutningen af visse felter.";
                    break;
                case AnalysisTestType.FORMAT:
                    Type = NotificationType.AnalysisErrorFormat;
                    Message = "Data har ikke det af datatypen påkrævede format.";
                    break;
                case AnalysisTestType.OVERFLOW:
                    Type = NotificationType.AnalysisErrorOverflow;
                    Message = "Data overskrider den maksimale længde defineret af datatypen.";
                    break;
                case AnalysisTestType.REGEX:
                    Type = NotificationType.AnalysisErrorRegex;
                    Message = null;
                    break;
                case AnalysisTestType.UNDERFLOW:
                    Type = NotificationType.AnalysisErrorUnderflow;
                    Message = "Data når ikke den minimale længde defineret af datatypen.";
                    Severity = Severity.Hint;
                    break;
                case AnalysisTestType.REPEATING_CHAR:
                    Severity = Severity.Hint;
                    var repcharTest = (Test.RepeatingChar) test;
                    var strB = new StringBuilder();
                    foreach (KeyValuePair<string, int> pair in repcharTest.Maximum)
                        strB.AppendFormat($"{pair.Key}({pair.Value}).");
                    Message = strB.ToString(); 
                    Type = NotificationType.AnalysisErrorRepeatingChar;
                    break;
                case AnalysisTestType.UNALLOWED_KEYWORD:
                    Severity = Severity.Hint;
                    var keywordTest = (Test.SuspiciousKeyword)test;
                    var entriesFound = keywordTest.Keywords;//.Where(pair => !pair.Value.Equals(0));
                    var keysFound = keywordTest.Keywords
                        .Where(entry => entry.Value != 0)
                        .Select(entry => entry.Key)
                        .ToList();
                    Message = keysFound.Count() == 0  ?  null : string.Join(" ", keysFound);
                    Type = NotificationType.AnalysisErrorUnallowedKeyword;
                    break;
            }

            Column = column;
            Count = 1;
            Post = post;
        }
    }


    public class ColumnParsingErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.ColumnTypeError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get; private set; }
        public Table Table { get; private set; }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public ColumnParsingErrorNotification(Table table, string message)
        {
            Table = Table;
            Message = message;
        }
    }

    public class ColumnTypeErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.ColumnTypeError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get; private set; }
        public Table Table { get { return Column.Table; } }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public ColumnTypeErrorNotification(Column column, string message)
        {
            Message = message;
            Column = column;
        }
    }

    public class TableRowCountNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.TableRowCountError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get { return null; } }
        public Table Table { get; private set; }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public TableRowCountNotification(Table table, int actualCount)
        {
            Table = table;
            Message = $"{actualCount} rækker i {Table.Folder}, {table.Rows} rækker defineret i tableIndex";
        }
    }

    public class ForeignKeyTypeErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.ForeignKeyTypeError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get { return null; } }
        public Table Table { get; private set; }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public ForeignKeyTypeErrorNotification(ForeignKey foreignKey)
        {
            Table = foreignKey.Table;
            Reference typeErrorReference = foreignKey.References.First(reference => reference.Column.ParameterizedDataType.CompareTo(reference.ReferencedColumn.ParameterizedDataType) != 0);
            Message = $"{foreignKey.Name} refererer {typeErrorReference.Column} til {typeErrorReference.ReferencedColumn} i {typeErrorReference.ReferencedColumn.Table}";
        }
    }

    public class XmlErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.XmlError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get { return null; } }
        public Table Table { get { return null; } }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public XmlErrorNotification(string message)
        {
            Message = $"Xml-validering gav følgende meddelelse: {message}";
        }
    }

    public class SuggestionNotification : INotification
    {
        public NotificationType Type { get; private set; }
        public Severity Severity { get { return Severity.Hint; } }
        public Column Column { get; private set; }
        public Table Table { get { return Column.Table; } }
        public string Header { get; private set; }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public SuggestionNotification(Column column, ParameterizedDataType suggestion)
        {
            Column = column;

            if (column.ParameterizedDataType.DataType == suggestion.DataType)
            {
                Type = NotificationType.ParameterSuggestion;
            }
            else
            {
                Type = NotificationType.DataTypeSuggestion;
            }

            Message = suggestion.ToString();
        }
    }

    public class ForeignKeyTestErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.ForeignKeyTestError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get { return null; } }
        public Table Table { get; private set; }
        public string Header { get; private set; }
        public string Message { get; private set; }
        public int? Count { get; private set; }
        public ForeignKey ForeignKey { get; private set; }
        public IDictionary<ForeignKeyValue, int> ErrorValues { get; private set; }

        public ForeignKeyTestErrorNotification(ForeignKey foreignKey, int count, IDictionary<ForeignKeyValue, int> errorValues)
        {
            ForeignKey = foreignKey;
            Table = foreignKey.Table;
            Count = count;
            Message = $"{foreignKey.Name} refererer til værdier der ikke findes i {foreignKey.ReferencedTable}";
            ErrorValues = errorValues;
        }
    }

    public class ForeignKeyTestBlankNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.ForeignKeyTestBlank; } }
        public Severity Severity { get { return Severity.Hint; } }
        public Column Column { get { return null; } }
        public Table Table { get; private set; }
        public string Header { get; private set; }
        public string Message { get; private set; }
        public int? Count { get; private set; }
        public ForeignKey ForeignKey { get; private set; }

        public ForeignKeyTestBlankNotification(ForeignKey foreignKey, int count)
        {
            ForeignKey = foreignKey;
            Table = foreignKey.Table;
            Count = count;
            Message = $"Blanke (NULL-værdier) refereres i {foreignKey.Name} til {foreignKey.ReferencedTable}";
        }
    }

    public class ForeignKeyReferencedTableMissingNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.ForeignKeyReferencedTableMissing; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get { return null; } }
        public Table Table { get; private set; }
        public string Header { get; private set; }
        public string Message { get; private set; }
        public int? Count { get; private set; }
        public ForeignKey ForeignKey { get; private set; }

        public ForeignKeyReferencedTableMissingNotification(ForeignKey foreignKey)
        {
            Table = foreignKey.Table;
            Message = $"{foreignKey.Name} referer til tabellen \"{foreignKey.ReferencedTableName}\", der ikke kan findes.";
            ForeignKey = foreignKey;
        }
    }

    public static class NotificationsUtility
    {
        public static string NotificationTypeToString(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.AnalysisErrorBlank:
                    return AnalysisUtility.AnalysisTestTypeToString(AnalysisTestType.BLANK);
                case NotificationType.AnalysisErrorFormat:
                    return AnalysisUtility.AnalysisTestTypeToString(AnalysisTestType.FORMAT);
                case NotificationType.AnalysisErrorOverflow:
                    return AnalysisUtility.AnalysisTestTypeToString(AnalysisTestType.OVERFLOW);
                case NotificationType.AnalysisErrorUnderflow:
                    return AnalysisUtility.AnalysisTestTypeToString(AnalysisTestType.UNDERFLOW);
                case NotificationType.AnalysisErrorRegex:
                    return AnalysisUtility.AnalysisTestTypeToString(AnalysisTestType.REGEX);
                case NotificationType.AnalysisErrorUnallowedKeyword:
                    return AnalysisUtility.AnalysisTestTypeToString(AnalysisTestType.UNALLOWED_KEYWORD);
                case NotificationType.AnalysisErrorRepeatingChar:
                    return AnalysisUtility.AnalysisTestTypeToString(AnalysisTestType.REPEATING_CHAR);
                case NotificationType.DataTypeIllegalAlias:
                    return "Ulovlig datatypeforkortelse";
                case NotificationType.ColumnParsing:
                    return "Feltindlæsningsfejl";
                case NotificationType.ColumnTypeError:
                    return "Datatypefejl";
                case NotificationType.TableRowCountError:
                    return "Tabelrækkeantalsfejl";
                case NotificationType.ForeignKeyTypeError:
                    return "Fremmednøgledatatypefejl";
                case NotificationType.XmlError:
                    return "Xml-valideringsfejl";
                case NotificationType.ParameterSuggestion:
                    return "Parameterforslag";
                case NotificationType.DataTypeSuggestion:
                    return "Datatypeforslag";
                case NotificationType.ForeignKeyTestError:
                    return "Fremmednøgletestfejl";
                case NotificationType.ForeignKeyTestBlank:
                    return "Fremmednøgletestfejl med blanke værdier";
                default:
                    return null;
            }
        }

        public static void WriteHTML(StreamWriter writer, ArchiveVersion archiveVersion, bool groupByTables, IEnumerable<CollectionViewGroup> NotificationGroups, DateTime now)
        {
            writer.WriteLine("<!doctype html>");
            writer.WriteLine("<html>");
            writer.WriteLine("<head>");
            writer.Write(@"<style>
.sort {display:inline-block; width: 0; height: 0; border-left: 8px solid transparent; border-right: 8px solid transparent; }
.asc {border-bottom: 10px solid black;}
.desc {border-top: 10px solid black;}
</style>
<script>
function sortBy(tableId, sortColumnIndex) {
var table = document.getElementById(tableId).nextElementSibling;
var sortTypeElem = table.children[0].children[sortColumnIndex].getElementsByTagName('strong')[0];
var sortType = '';
if (sortTypeElem != undefined) {
    sortType = sortTypeElem.innerText;
}
var rows = Array.prototype.slice.call(table.children, 1);
var glyphs = table.getElementsByClassName('sort');
for (var i = 0; i < glyphs.length; i++) {
	glyphs[i].remove();
}
if (table.lastSortType == sortType) {
	table.sortAscending = !table.sortAscending;
	rows.reverse();
} else {
	table.sortAscending = true;
	table.lastSortType = sortType;
	rows.sort(getSortFunc(sortType, sortColumnIndex)); 
}
addGlyph(table, sortColumnIndex);
readdRows(rows);
}

function getSortFunc(sortType, sortColumnIndex) {
switch (sortType) {
	case 'Felt':
		return function(row1, row2) {
			var idx = sortColumnIndex;
			var field1 = row1.children[idx].innerText;
			var field2 = row2.children[idx].innerText;
			if (field1 == '-') {
				return -1;
			}
			if (field2 == '-') {
				return 1;
			}
			var f1 = parseInt(field1.substr(2, field1.indexOf(':')-2));
			var f2 = parseInt(field2.substr(2, field2.indexOf(':')-2));
			return f1-f2;
		};
		break;
	case 'Tabel':
		return function(row1, row2) {
			var idx = sortColumnIndex;
			var field1 = row1.children[idx].innerText;
			var field2 = row2.children[idx].innerText;
			if (field1 == '-') {
				return -1;
			}
			if (field2 == '-') {
				return 1;
			}
			var f1 = parseInt(field1.substr(6, field1.indexOf(':')-6));
			var f2 = parseInt(field2.substr(6, field2.indexOf(':')-6));
			return f1-f2;
		};
		break;
    case 'Forekomster':
        return function(row1, row2) {
            var idx = sortColumnIndex;
			var field1 = row1.children[idx].innerText;
			var field2 = row2.children[idx].innerText;
			if (field1 == '-') {
				return -1;
			}
			if (field2 == '-') {
				return 1;
			}
            return parseInt(field1) - parseInt(field2);
        };
        break;
	default:
		return function(row1, row2) {
			var idx = sortColumnIndex;
			var field1 = row1.children[idx].innerText;
			var field2 = row2.children[idx].innerText;
			if (field1 == '-') {
				return -1;
			}
			if (field2 == '-') {
				return 1;
			}
			return field1.localeCompare(field2);
		};
		break;
}
	
}

function readdRows(rows) {
for (var i = 0; i < rows.length; i++) {
	var parent = rows[i].parentNode;
	var detached = parent.removeChild(rows[i]);
	parent.appendChild(detached); 
}
}

function addGlyph(table, columnIndex) {
var glyph = document.createElement('span');
glyph.classList.add('sort');
if (table.sortAscending) {
    glyph.classList.add('asc');
} else {
    glyph.classList.add('desc');
}
table.children[0].children[columnIndex].appendChild(glyph);
}
</script>");
            writer.WriteLine($"<title>{archiveVersion.Id} - HardHorn Log</title>");
            writer.WriteLine("</head>");
            writer.WriteLine("<body style=\"font-family: verdana, sans-serif;\">");
            writer.WriteLine($"<h1>{archiveVersion.Id} - HardHorn Log</h1>");
            writer.WriteLine($"<p><strong>Tidspunkt:</strong> {now}</p>");
            writer.WriteLine("<h2 id=\"oversigt\">Oversigt</h2>");
            writer.WriteLine("<ul>");
            foreach (CollectionViewGroup group in NotificationGroups)
            {
                writer.WriteLine($"<li><a href=\"#{HttpUtility.HtmlEncode(group.Name)}\">{HttpUtility.HtmlEncode(group.Name)} ({group.ItemCount} punkter)</a></li>");
            }
            writer.WriteLine("</ul>");
            writer.WriteLine("<h2>Rapport</h2>");
            if (groupByTables) // Table groups
            {
                foreach (CollectionViewGroup group in NotificationGroups)
                {
                    writer.WriteLine("<div>");
                    writer.WriteLine($"<h3 id=\"{HttpUtility.HtmlEncode(group.Name)}\">{HttpUtility.HtmlEncode(group.Name)}&nbsp;<span style=\"font-weight: normal; font-size: 12pt;\"><a href=\"#oversigt\">[til oversigt]</a></span></h3>");
                    writer.WriteLine("<div style=\"display: table\">");
                    writer.WriteLine("<div style=\"display: table-row\">");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 0)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"></div>");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 1)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Felt</strong></div>");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 2)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Kategori</strong></div>");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 3)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Forekomster</strong></div>");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 4)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Besked</strong></div>");
                    writer.WriteLine("</div>");
                    foreach (INotification notification in group.Items)
                    {
                        writer.WriteLine("<div style=\"display: table-row\">");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{(notification.Severity == Severity.Hint ? "<b style=\"background: yellow;\">!</b>" : "<b style=\"background: red; color: white;\">X</b>")}</div>");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Column?.ToString() ?? "-")}</div>");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(NotificationTypeToString(notification.Type)?.ToString() ?? "-")}</div>");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{notification.Count?.ToString() ?? "-"}</div>");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Message?.ToString() ?? "-")}</div>");
                        writer.WriteLine("</div>");
                    }
                    writer.WriteLine("</div>");
                    writer.WriteLine("</div>");
                }
            }
            else // Category groups
            {
                foreach (CollectionViewGroup group in NotificationGroups)
                {
                    writer.WriteLine("<div>");
                    writer.WriteLine($"<h3 id=\"{HttpUtility.HtmlEncode(group.Name)}\">{HttpUtility.HtmlEncode(group.Name)}&nbsp;<span style=\"font-weight: normal; font-size: 12pt;\"><a href=\"#oversigt\">[til oversigt]</a></span></h3>");
                    writer.WriteLine("<div style=\"display: table\">");
                    writer.WriteLine("<div style=\"display: table-row\">");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 0)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"></div>");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 1)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Tabel</strong></div>");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 2)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Felt</strong></div>");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 3)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Forekomster</strong></div>");
                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 4)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Besked</strong></div>");
                    writer.WriteLine("</div>");
                    foreach (INotification notification in group.Items)
                    {
                        writer.WriteLine("<div style=\"display: table-row\">");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{(notification.Severity == Severity.Hint ? "<b style=\"background: yellow;\">!</b>" : "<b style=\"background: red; color: white;\">X</b>")}</div>");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Table?.ToString() ?? "-")}</div>");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Column?.ToString() ?? "-")}</div>");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{notification.Count?.ToString() ?? "-"}</div>");
                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Message?.ToString() ?? "-")}</div>");
                        writer.WriteLine("</div>");
                    }
                    writer.WriteLine("</div>");
                    writer.WriteLine("</div>");
                }
            }
            writer.WriteLine("</body>");
            writer.WriteLine("</html>");
        }
    }
}
