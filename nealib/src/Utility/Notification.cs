﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;

using NEA.Analysis;
using NEA.Archiving;

namespace NEA.Utility
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
        AnalysisErrorRepeatingCharacter,
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
                    uint col_treshold = column.ParameterizedDataType.Parameter.Length * 100 / 95;
                    Severity = Severity.Hint;
                    var repcharTest = (Test.RepeatingCharacter)test;

                    SortedList<string, int> critical = new SortedList<string, int>();
                    SortedList<string, int> rest = new SortedList<string, int>();

                    foreach (KeyValuePair<string, int> pair in repcharTest.Maximum)
                    {
                        if (pair.Value > col_treshold)
                            critical.Add(pair.Key, pair.Value);
                        else
                            rest.Add(pair.Key, pair.Value);
                    }
                    Message = "";
                    if (critical.Any())
                        Message += "Over tærskel: " + string.Join(", ", critical.Select(pair => $"{pair.Key}: {pair.Value}"));
                    if (rest.Any())
                    {
                        if (Message.Length > 0)
                            Message += " ";
                        Message += "Resterende: " + string.Join(", ", rest.Select(pair => $"{pair.Key}: {pair.Value}"));
                    }
                    Type = NotificationType.AnalysisErrorRepeatingCharacter;
                    break;
                case AnalysisTestType.UNALLOWED_KEYWORD:
                    Severity = Severity.Hint;
                    var keywordTest = (Test.SuspiciousKeyword)test;
                    var entriesFound = keywordTest.Keywords;//.Where(pair => !pair.Value.Equals(0));
                    var keysFound = keywordTest.Keywords
                        .Where(entry => entry.Value != 0)
                        .Select(entry => entry.Key)
                        .ToList();
                    Message = keysFound.Count() == 0 ? null : string.Join(" ", keysFound);
                    Type = NotificationType.AnalysisErrorUnallowedKeyword;
                    break;
                case AnalysisTestType.HTML_TAG:
                    Severity = Severity.Hint;
                    var htmlTest = (Test.HtmlEntity)test;
                    Message = htmlTest.Value;
                    Type = NotificationType.AnalysisErrorUnallowedKeyword;
                    break;
                case AnalysisTestType.ENTITY_CHAR_REF:
                    Severity = Severity.Hint;
                    var entityTest = (Test.EntityCharRef)test;
                    Message = entityTest.CharRef;
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
                case NotificationType.AnalysisErrorRepeatingCharacter:
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
                    return "Fremmednøgletestfejl med NULL-værdier";
                default:
                    return null;
            }
        }

    }
}