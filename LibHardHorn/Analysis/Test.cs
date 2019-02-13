using HardHorn.Archiving;
using HardHorn.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HardHorn.Analysis
{
    public abstract class Test
    {
        public static Regex date_regex = new Regex(@"^(\d\d\d\d)-(\d\d)-(\d\d)$", RegexOptions.Compiled);
        public static Regex timestamp_regex = new Regex(@"^(\d\d\d\d)-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:.(\d+))?$", RegexOptions.Compiled);
        public static Regex time_regex = new Regex(@"^(\d\d):(\d\d):(\d\d)(?:.(\d+))?$", RegexOptions.Compiled);
        public static Regex timestamp_timezone_regex = new Regex(@"^(\d\d\d\d)-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:.(\d+))?((?:\+|-)(\d\d):(\d\d)|Z)$", RegexOptions.Compiled);
        public static Regex time_timezone_regex = new Regex(@"^(\d\d):(\d\d):(\d\d)(?:.(\d+))?((?:\+|-)(\d\d):(\d\d)|Z)$", RegexOptions.Compiled);
        public static Regex integer_regex = new Regex(@"^-?\d+$", RegexOptions.Compiled);
        public static Regex numeric_regex = new Regex(@"^-?\d+(\.\d+)?$", RegexOptions.Compiled);
        public static Regex boolean_regex = new Regex(@"^(0|1|true|false)$", RegexOptions.Compiled);
        public static Regex float_regex = new Regex(@"^-?\d+(\.\d+)?([eE][+-]\d+)?$", RegexOptions.Compiled);
        public static int[] months = new int[] { 31, 29, 31, 30, 31, 30, 31, 33, 30, 31, 30, 31 };
        //public static Regex numeric_char_ref = new Regex(@"^&(?:#([0-9]+)|#x([0-9a-fA-F]+))$", RegexOptions.Compiled);
        public static Regex entity_regex = new Regex(@"&(?!(amp;|apos;|lt;|gt;|quot;))[\w | #]*;", RegexOptions.Compiled);
        public static Regex char_repeating_regex = new Regex(@"([^ ])\1{12,}", RegexOptions.Compiled);
        public static Regex html_opentag_regex = new Regex(@"<[\w]*>", RegexOptions.Compiled);
        public static Regex html_closetag_regex = new Regex(@"<\/[\w]*>", RegexOptions.Compiled);


        public enum Result
        {
            ERROR,
            OKAY
        }

        public int ErrorCount { get; private set; }
        public abstract AnalysisTestType Type { get; }
        public string Name { get { return Type.ToString(); } }
        public static int MAX_ERROR_POSTS = 50;

        List<Post> _posts = new List<Post>();
        public IEnumerable<Post> ErrorPosts { get { return _posts; } }

        public void Add(Post post)
        {
            if (ErrorCount < MAX_ERROR_POSTS)
            {
                _posts.Add(post);
            }
            ErrorCount++;
        }

        public static bool InvalidTime(int hour, int minute, int second)
        {
            return hour > 23 ||
                   hour < 0 ||
                   minute > 59 ||
                   minute < 0 ||
                   second > 59 ||
                   second < 0;
        }

        public static bool InvalidDate(int year, int month, int day)
        {
            bool leap = (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
            return year <= 0 ||
                   month > 12 ||
                   month < 1 ||
                   day < 1 ||
                   month == 2 && leap && day > months[2] - 1 ||
                   day > months[month - 1];
        }

        public Result Run(Post post, Column column, NotificationCallback notify)
        {
            if (post.IsNull)
                return Result.OKAY;

            var result = GetResult(post, column);
            if (result == Result.ERROR)
            {
                Add(post);
                notify?.Invoke(new AnalysisErrorNotification(this, column, post));
            }
            return result;
        }

        public abstract Result GetResult(Post post, Column column);

        #region Predefined format tests
        public static Test DateFormatTest()
        {
            return new Pattern(date_regex, m =>
            {
                int year = int.Parse(m[0].Groups[1].Value);
                int month = int.Parse(m[0].Groups[2].Value);
                int day = int.Parse(m[0].Groups[3].Value);

                if (InvalidDate(year, month, day))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }

        public static Test TimeFormatTest()
        {
            return new Pattern(time_regex, m =>
            {
                int hour = int.Parse(m[0].Groups[1].Value);
                int minute = int.Parse(m[0].Groups[2].Value);
                int second = int.Parse(m[0].Groups[3].Value);

                if (InvalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }

        public static Test TimeWithTimeZoneTest()
        {
            return new Pattern(time_timezone_regex, m =>
            {
                int hour = int.Parse(m[0].Groups[1].Value);
                int minute = int.Parse(m[0].Groups[2].Value);
                int second = int.Parse(m[0].Groups[3].Value);
                if (m[0].Groups[5].Value == "Z")
                {

                }
                else
                {
                    int tzHour = int.Parse(m[0].Groups[6].Value);
                    int tzMinute = int.Parse(m[0].Groups[7].Value);
                    if (tzHour > 12 || tzMinute > 59)
                        return Result.ERROR;
                }

                if (InvalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }

        public static Test TimestampWithTimeZoneFormatTest()
        {
            return new Pattern(timestamp_timezone_regex, m =>
            {
                int year = int.Parse(m[0].Groups[1].Value);
                int month = int.Parse(m[0].Groups[2].Value);
                int day = int.Parse(m[0].Groups[3].Value);
                int hour = int.Parse(m[0].Groups[4].Value);
                int minute = int.Parse(m[0].Groups[5].Value);
                int second = int.Parse(m[0].Groups[6].Value);
                if (m[0].Groups[8].Value == "Z")
                {

                }
                else
                {
                    int tzHour = int.Parse(m[0].Groups[9].Value);
                    int tzMinute = int.Parse(m[0].Groups[10].Value);
                    if (tzHour > 12 || tzMinute > 59)
                        return Result.ERROR;
                }

                if (InvalidDate(year, month, day) || InvalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }

        public static Test TimestampFormatTest()
        {
            return new Pattern(timestamp_regex, m =>
            {
                int year = int.Parse(m[0].Groups[1].Value);
                int month = int.Parse(m[0].Groups[2].Value);
                int day = int.Parse(m[0].Groups[3].Value);
                int hour = int.Parse(m[0].Groups[4].Value);
                int minute = int.Parse(m[0].Groups[5].Value);
                int second = int.Parse(m[0].Groups[6].Value);

                if (InvalidDate(year, month, day) || InvalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }
        // Test for keywords
        public class SuspiciousKeyword : Test
        {
            public override AnalysisTestType Type { get { return AnalysisTestType.UNALLOWED_KEYWORD; } }

            public Dictionary<string, int> Keywords = new Dictionary<string, int>
            {
                {"border", 0},
                {"span", 0},
                {"padding", 0},
                {"font", 0 },
                {"color", 0},
                {"font-family", 0},
                {"font-size", 0},
                {"font-style", 0},
                {"style", 0},
                {"font-weight", 0},
                {"background", 0},
                {"spacing", 0},
                {"line-height", 0},
                {"table", 0},
                {"margin", 0},
                {"body", 0},
                {"div", 0},
                {"px", 0},
                {"list", 0},
                {"text-align", 0},
                {"text-decoration", 0},
                {"text-indent", 0},
                {"text-transform", 0},
                {"meta", 0},
                {"title", 0},
                {"header", 0},
                {"position", 0},
                {"template", 0},
                {"form", 0},
                {"html", 0},
                {"head", 0},
                {"h1", 0},
                {"h2", 0},
                {"h3", 0},
                {"display", 0},
            };

            public void ContainsKeywords(string HugeText)
            {
                Keywords.Keys.ToList().ForEach(x => Keywords[x] = 0);
                    foreach (var key in Keywords.Keys.ToList())
                    {
                        
                        if (HugeText.Contains(key))
                        {
                            int keyInd = HugeText.IndexOf(key);
                            int indBeforeKey = keyInd - 1;
                            int indAfterKey = keyInd + key.Length;
                            bool isLetterBef = Char.IsLetter(HugeText[indBeforeKey]);
                            bool isLetterAft = Char.IsLetter(HugeText[indAfterKey]);

                            // keyword is not part of compound word
                            if ( !(isLetterBef || isLetterAft) )
                            {
                                Keywords[key] += 1;
                            }
                        }
                    }
                
            }

            public override Result GetResult(Post post, Column column)
            {
                var sumBefore = Keywords.Values.Sum();
                ContainsKeywords(post.Data);
                if (Keywords.Values.Sum() > sumBefore)
                    return Result.ERROR;
                    
                else
                    return Result.OKAY;
            }
        }

        #endregion
        // Test for all html code
        public class HtmlEntity : Test
        {
            public override AnalysisTestType Type { get { return AnalysisTestType.HTML_TAG; } }

            public string Value { get; set; }

            public override Result GetResult(Post post, Column column)
            {
                Match match;
                match = html_opentag_regex.Match(post.Data);
                // opening tag-like structure found
                if (match.Success)
                {
                    Value = match.Value;
                    return Result.ERROR;
                }
                // closing tag-like structure found
                match = html_closetag_regex.Match(post.Data);
                if (match.Success)
                {
                    Value = match.Value;
                    return Result.ERROR;
                }
                return Result.OKAY;
            }
        }
        // Find all repeating characters
        public class RepeatingChar : Test
        {
            public override AnalysisTestType Type { get { return AnalysisTestType.REPEATING_CHAR; } }

            public string CharRepeating { get; set; }
            public Dictionary<string, int> Maximum = new Dictionary<string, int>();

            public override Result GetResult(Post post, Column column)
            {
                MatchCollection matches = char_repeating_regex.Matches(post.Data);
                
                if (matches.Count != 0)
                {
                    foreach(Match match in matches)
                    {
                        CharRepeating = match.Groups[1].ToString();
                        int ind = match.Index;
                        var matchLen = match.Value.Length;
                      
                        if (!Maximum.ContainsKey(CharRepeating))
                            Maximum.Add(CharRepeating, matchLen);
                        else
                            if (matchLen > Maximum[CharRepeating])
                            Maximum[CharRepeating] = matchLen;
                    }
                    return Result.ERROR;
                }
                return Result.OKAY;
            }
        }
        // 
        public class EntityCharRef : Test
        {
            public override AnalysisTestType Type { get { return AnalysisTestType.ENTITY_CHAR_REF; } }

            public string CharRef { get; set; }

            public override Result GetResult(Post post, Column column)
            {
                Match match;

                match = entity_regex.Match(post.Data);
                if (match.Success)
                {
                    CharRef = match.Groups[0].ToString();
                    return Result.ERROR;
                }
                return Result.OKAY;
            }
        }

        public class Overflow : Test
        {
            public override AnalysisTestType Type { get { return AnalysisTestType.OVERFLOW; } }

            public override Result GetResult(Post post, Column column)
            {
                Match match;
                bool overflow = false;

                switch (column.ParameterizedDataType.DataType)
                {
                    case DataType.NATIONAL_CHARACTER:
                    case DataType.CHARACTER:
                    case DataType.NATIONAL_CHARACTER_VARYING:
                    case DataType.CHARACTER_VARYING:

                        overflow = post.Data.Length > column.ParameterizedDataType.Parameter.Length;
                        break;
                    case DataType.DECIMAL:
                        var components = post.Data.Split('.');
                        // Remove negation
                        if (components.Length > 0 && components[0][0] == '-')
                        {
                            components[0] = components[0].Substring(1);
                        }
                        // No separator
                        if (components.Length == 1)
                            overflow = components[0].Length > column.ParameterizedDataType.Parameter.Precision;
                        // With separator
                        if (components.Length == 2)
                            overflow = components[0].Length + components[1].Length > column.ParameterizedDataType.Parameter.Precision || components[1].Length > column.ParameterizedDataType.Parameter.Scale;
                        break;
                    case DataType.TIME:
                        match = time_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[4].Length > column.ParameterizedDataType.Parameter.Precision;
                        }
                        break;
                    case DataType.TIME_WITH_TIME_ZONE:
                        match = time_timezone_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[4].Length > column.ParameterizedDataType.Parameter.Precision;
                        }
                        break;
                    case DataType.TIMESTAMP:
                        match = timestamp_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[7].Length > column.ParameterizedDataType.Parameter.Precision;
                        }
                        break;
                    case DataType.TIMESTAMP_WITH_TIME_ZONE:
                        match = timestamp_timezone_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[7].Length > column.ParameterizedDataType.Parameter.Precision;
                        }
                        break;
                }

                return overflow ? Result.ERROR : Result.OKAY;
            }
        }

        public class Underflow : Test
        {
            public override AnalysisTestType Type { get { return AnalysisTestType.UNDERFLOW; } }

            public override Result GetResult(Post post, Column column)
            {
                var underflow = false;
                switch (column.ParameterizedDataType.DataType)
                {
                    case DataType.NATIONAL_CHARACTER:
                    case DataType.CHARACTER:
                        underflow = post.Data.Length < column.ParameterizedDataType.Parameter.Length;
                        break;
                }
                return underflow ? Result.ERROR : Result.OKAY;
            }
        }

        public class Blank : Test
        {
            static char[] whitespaces = new char[]
            {
                '\u0009',
                '\u000A',
                '\u000B',
                '\u000C',
                '\u000D',
                '\u0020',
                '\u0085',
                '\u00A0',
                '\u1680',
                '\u2000',
                '\u2001',
                '\u2002',
                '\u2003',
                '\u2004',
                '\u2005',
                '\u2006',
                '\u2007',
                '\u2008',
                '\u2009',
                '\u200A',
                '\u2028',
                '\u2029',
                '\u202F',
                '\u205F',
                '\u3000',
                '\u180E',
                '\u200B',
                '\u200C',
                '\u200D',
                '\u2060',
                '\uFEFF'
            };
            public override AnalysisTestType Type { get { return AnalysisTestType.BLANK; } }

            bool IsWhitespace(char c)
            {
                return whitespaces.Any(w => c == w);
                //return c == ' ' || c == '\n' || c == '\t' || c == '\r';
            }

            public override Result GetResult(Post post, Column column)
            {
                return post.Data.Length > 0 && (IsWhitespace(post.Data[0]) || IsWhitespace(post.Data[post.Data.Length - 1])) ? Result.ERROR : Result.OKAY;
            }
        }

        public class Pattern : Test
        {
            AnalysisTestType _type;
            public override AnalysisTestType Type { get { return _type; } }
            public new string Name
            {
                get
                {
                    if (Type == AnalysisTestType.REGEX)
                    {
                        return $"Regex \"{Regex.ToString()}\"";
                    }
                    else
                    {
                        return base.Name;
                    }
                }
            }

            public Regex Regex { get; private set; }
            public Func<MatchCollection, Result> HandleMatches { get; private set; }

            public Pattern(Regex regex, Func<MatchCollection, Result> handleMatches = null, AnalysisTestType type = AnalysisTestType.REGEX)
            {
                Regex = regex;
                HandleMatches = handleMatches;
                _type = type;
            }

            public override Result GetResult(Post post, Column column)
            {
                var matches = Regex.Matches(post.Data);

                if (HandleMatches == null || matches.Count == 0)
                {
                    return matches.Count > 0 ? Result.OKAY : Result.ERROR;
                }

                return HandleMatches(matches);
            }
        }

        internal void Clear()
        {
            ErrorCount = 0;
            _posts.Clear();
        }
    }
}
