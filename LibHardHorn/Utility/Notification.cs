using HardHorn.Analysis;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        LoadingException,
        AnalysisError,
        TableRowCountError,
        ForeignKeyError,
        ColumnParsing,
        Suggestion,  
        HtmlEntity
    }

    public delegate void NotificationCallback(INotification notification);


    public interface INotification
    {
        NotificationType Type { get; }
        Severity Severity { get;  }
        Column Column { get; }
        Table Table { get; }
        string Header { get; }
        string Message { get; }
        int? Count { get; }
    }

    // only function to check tables 
    // include error check here?
    public class AnalysisErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.AnalysisError; } }
        public Severity Severity { get; private set; }
        public Column Column { get; private set; }
        public Table Table { get { return Column.Table; } }
        public string Header { get; private set; }
        public string Message { get; private set; }
        public int? Count { get; private set; }
        public AnalysisTestType TestType { get; private set; }

        public AnalysisErrorNotification(Test test, Column column, Post post)
        {
            TestType = test.Type;
            switch (TestType)
            {
                case AnalysisTestType.HTML_TAG:
                    var htmlTest = (Test.HtmlEntity) test;
                    Message = htmlTest.Value;
                    break;
                case AnalysisTestType.ENTITY_CHAR_REF:
                    var charrefTest = (Test.EntityCharRef) test;
                    Message = charrefTest.CharRef;
                    break;
                case AnalysisTestType.REPEATING_CHAR:
                    var repcharTest = (Test.RepeatingChar) test;
                    Message = repcharTest.CharRepeating;
                    break;
                case AnalysisTestType.UNALLOWED_KEYWORD:
                    var keywordTest = (Test.SuspiciousKeyword)test;
                    var entriesFound = keywordTest.Keywords;//.Where(pair => !pair.Value.Equals(0));
                    var keysFound = keywordTest.Keywords
                        .Where(entry => entry.Value != 0)
                        .Select(entry => entry.Key)
                        .ToArray();
                    Message = String.Join(" ", keysFound);
                    break;
            }

            Severity = test.Type == AnalysisTestType.UNDERFLOW ? Severity.Hint : Severity.Error;
            Header = $"Test ({test.Type})";
            Column = column;
            Count = 1;
        }
    }

    public class HtmlEntityErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.HtmlEntity; } }
        public Severity Severity { get; }
        public Column Column { get; private set; }
        public Table Table { get; private set; }
        public string Header { get; private set; }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public HtmlEntityErrorNotification(string message, Column column, Post post)
        {
            Header = "Fix this, some error";
            Message = message;
        }
    }

    public class ColumnParsingErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.ColumnTypeError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get; private set; }
        public Table Table { get; private set; }
        public string Header { get; private set; }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public ColumnParsingErrorNotification(Table table, string message)
        {
            Table = Table;
            Header = $"Feltindlæsningsfejl";
            Message = message;
        }
    }

    public class ColumnTypeErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.ColumnTypeError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get; private set; }
        public Table Table { get { return Column.Table; } }
        public string Header { get; private set; }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public ColumnTypeErrorNotification(Column column, string message)
        {
            Header = $"Datatypefejl";
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
        public string Header { get; private set; }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public TableRowCountNotification(Table table, int actualCount)
        {
            Table = table;
            Header = $"Tabelrækkeantalsfejl";
            Message = $"{actualCount} rækker i {Table.Folder}, {table.Rows} rækker defineret i tableIndex";
        }
    }

    public class ForeignKeyErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.ForeignKeyError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get { return null; } }
        public Table Table { get; private set; }
        public string Header { get { return "Fremmednøglefejl"; } }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public ForeignKeyErrorNotification(ForeignKey foreignKey)
        {
            Table = foreignKey.Table;
            Message = foreignKey.Name;
        }
    }

    public class XmlErrorNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.XmlError; } }
        public Severity Severity { get { return Severity.Error; } }
        public Column Column { get { return null; } }
        public Table Table { get { return null; } }
        public string Header { get { return "Xml-valideringsfejl"; } }
        public string Message { get; private set; }
        public int? Count { get { return null; } }

        public XmlErrorNotification(string message)
        {
            Message = message;
        }
    }

    public class SuggestionNotification : INotification
    {
        public NotificationType Type { get { return NotificationType.Suggestion; } }
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
                Header = "Parameterforslag";
            }
            else
            {
                Header = "Datatypeforslag";
            }

            Message = suggestion.ToString();
        }
    }
}
