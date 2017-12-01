using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HardHorn.Analysis
{
    public abstract class Test : INotifyPropertyChanged
    {
        public static readonly int MAX_ERROR_POSTS = 50;

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyOfPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public enum Result
        {
            ERROR,
            OKAY
        }

        public int ErrorCount { get; private set; }
        public abstract AnalysisTestType Type { get; }

        List<Post> _posts = new List<Post>();
        public IEnumerable<Post> ErrorPosts { get { return _posts; } }

        public void Add(Post post)
        {
            if (ErrorCount < MAX_ERROR_POSTS)
            {
                _posts.Add(post);
                NotifyOfPropertyChanged("ErrorPosts");
            }
            ErrorCount++;
            NotifyOfPropertyChanged("ErrorCount");
        }

        public Result Run(Post post, Column column)
        {
            if (post.IsNull)
                return Result.OKAY;

            var result = GetResult(post, column);
            if (result == Result.ERROR)
            {
                Add(post);
            }
            return result;
        }

        public abstract Result GetResult(Post post, Column column);

        #region Predefined format tests
        public static Test DateFormatTest()
        {
            return new Pattern(Analyzer.date_regex, m => {
                int year = int.Parse(m[0].Groups[1].Value);
                int month = int.Parse(m[0].Groups[2].Value);
                int day = int.Parse(m[0].Groups[3].Value);

                if (Analyzer.invalidDate(year, month, day))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }

        public static Test TimeFormatTest()
        {
            return new Pattern(Analyzer.time_regex, m => {
                int hour = int.Parse(m[0].Groups[1].Value);
                int minute = int.Parse(m[0].Groups[2].Value);
                int second = int.Parse(m[0].Groups[3].Value);

                if (Analyzer.invalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }

        public static Test TimeWithTimeZoneTest()
        {
            return new Pattern(Analyzer.time_timezone_regex, m => {
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

                if (Analyzer.invalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }

        public static Test TimestampWithTimeZoneFormatTest()
        {
            return new Pattern(Analyzer.timestamp_timezone_regex, m => {
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

                if (Analyzer.invalidDate(year, month, day) || Analyzer.invalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }

        public static Test TimestampFormatTest()
        {
            return new Pattern(Analyzer.timestamp_regex, m =>
            {
                int year = int.Parse(m[0].Groups[1].Value);
                int month = int.Parse(m[0].Groups[2].Value);
                int day = int.Parse(m[0].Groups[3].Value);
                int hour = int.Parse(m[0].Groups[4].Value);
                int minute = int.Parse(m[0].Groups[5].Value);
                int second = int.Parse(m[0].Groups[6].Value);

                if (Analyzer.invalidDate(year, month, day) || Analyzer.invalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, AnalysisTestType.FORMAT);
        }
        #endregion

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
                        overflow = post.Data.Length > column.ParameterizedDataType.Parameter[0];
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
                            overflow = components[0].Length > column.ParameterizedDataType.Parameter[0];
                        // With separator
                        if (components.Length == 2)
                            overflow = components[0].Length + components[1].Length > column.ParameterizedDataType.Parameter[0] || components[1].Length > column.ParameterizedDataType.Parameter[1];
                        break;
                    case DataType.TIME:
                        match = Analyzer.time_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[4].Length > column.ParameterizedDataType.Parameter[0];
                        }
                        break;
                    case DataType.TIME_WITH_TIME_ZONE:
                        match = Analyzer.time_timezone_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[4].Length > column.ParameterizedDataType.Parameter[0];
                        }
                        break;
                    case DataType.TIMESTAMP:
                        match = Analyzer.timestamp_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[7].Length > column.ParameterizedDataType.Parameter[0];
                        }
                        break;
                    case DataType.TIMESTAMP_WITH_TIME_ZONE:
                        match = Analyzer.timestamp_timezone_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[7].Length > column.ParameterizedDataType.Parameter[0];
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
                        underflow = post.Data.Length < column.ParameterizedDataType.Parameter[0];
                        break;
                }
                return underflow ? Result.ERROR : Result.OKAY;
            }
        }

        public class Blank : Test
        {
            public override AnalysisTestType Type { get { return AnalysisTestType.BLANK; } }

            bool IsWhitespace(char c)
            {
                return c == ' ' || c == '\n' || c == '\t' || c == '\r';
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
