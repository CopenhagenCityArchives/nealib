using HardHorn.Archiving;
using System;
using System.Collections.Generic;

namespace HardHorn.Analysis
{
    public class ColumnAnalysis
    {
        DateTime _lastErrorsEventTime = DateTime.Now;

        /// <summary>
        /// The number of errors found, when analyzing the column.
        /// </summary>
        public int ErrorCount { get; private set; }

        /// <summary>
        /// A dictionary of the test failures.
        /// </summary>
        public IDictionary<Test, IList<Tuple<Post, Exception>>> TestFailures { get; private set; }

        /// <summary>
        /// The tests to be performed on this column.
        /// </summary>
        public IList<Test> Tests { get; private set; }

        /// <summary>
        /// The suggestion for this column.
        /// </summary>
        public ParameterizedDataType SuggestedType { get; set; }

        /// <summary>
        /// The column to be analyzed.
        /// </summary>
        public Column Column { get; private set; }

        /// <summary>
        /// All posts so far analyzed are null.
        /// </summary>
        public bool AllNullSoFar { get; private set; }

        /// <summary>
        /// The first row has been analyzed.
        /// </summary>
        public bool FirstRowAnalyzed { get; set; }

        /// <summary>
        /// If true, this field will only have Character-type suggestions.
        /// </summary>
        public bool ForceCharacterType { get; set; }
        public Parameter CharacterMinParameter { get; private set; }
        public Parameter CharacterMaxParameter { get; private set; }

        /// <summary>
        /// Indicates whether all posts encountered so far are valid timestamps without time zones.
        /// </summary>
        public bool TimestampFormat { get; private set; }
        public Parameter TimestampMinParameter { get; private set; }
        public Parameter TimestampMaxParameter { get; private set; }

        /// <summary>
        /// Indicates whether all posts encountered so far are valid time without time zones.
        /// </summary>
        public bool TimeFormat { get; private set; }
        public Parameter TimeMinParameter { get; private set; }
        public Parameter TimeMaxParameter { get; private set; }

        /// <summary>
        /// Indicates whether all posts encountered so far are valid timestamps with time zones.
        /// </summary>
        public bool TimestampTimeZoneFormat { get; private set; }
        public Parameter TimestampTimeZoneMinParameter { get; private set; }
        public Parameter TimestampTimeZoneMaxParameter { get; private set; }

        /// <summary>
        /// Indicates whether all posts encounted so far are valid time with time zones.
        /// </summary>
        public bool TimeTimeZoneFormat { get; private set; }
        public Parameter TimeTimeZoneMinParameter { get; private set; }
        public Parameter TimeTimeZoneMaxParameter { get; private set; }

        /// <summary>
        /// Indicates whether all posts encountered so far are valid dates.
        /// </summary>
        public bool DateFormat { get; private set; }

        /// <summary>
        /// Indicates whether all posts analyzed so far are valid numerics.
        /// </summary>
        public bool NumericFormat { get; private set; }

        /// <summary>
        /// The maximum parameter for numerics encountered so far.
        /// </summary>
        public Parameter NumericMinParameter { get; private set; }

        /// <summary>
        /// The minimum parameter for numerics encountered so far.
        /// </summary>
        public Parameter NumericMaxParameter { get; private set; }

        /// <summary>
        /// Indicates whether all posts analyzed so far are valid floats.
        /// </summary>
        public bool FloatFormat { get; private set; }

        /// <summary>
        /// Indicates whether all posts analyzed so far are valid integers.
        /// </summary>
        public bool IntegerFormat { get; private set; }

        /// <summary>
        /// Construct a ColumnAlysis from a Column.
        /// </summary>
        /// <param name="column">The column.</param>
        public ColumnAnalysis(Column column)
        {
            FirstRowAnalyzed = false;
            AllNullSoFar = true;
            Column = column;

            ForceCharacterType = false;
            TimestampFormat = true;
            TimeFormat = true;
            TimestampTimeZoneFormat = true;
            TimeTimeZoneFormat = true;
            DateFormat = true;
            NumericFormat = true;
            FloatFormat = true;
            IntegerFormat = true;

            ErrorCount = 0;
            Tests = new List<Test>();
            TestFailures = new Dictionary<Test, IList<Tuple<Post, Exception>>>();
        }

        /// <summary>
        /// Apply the suggestion to the encapsulated column.
        /// </summary>
        public void ApplySuggestion()
        {
            if (SuggestedType != null)
            {
                Column.ParameterizedDataType.DataType = SuggestedType.DataType;
                Column.ParameterizedDataType.Parameter = SuggestedType.Parameter;
            }
        }

        /// <summary>
        /// Run all tests on a the given post.
        /// </summary>
        /// <param name="post">A post.</param>
        public void RunTests(Post post)
        {
            foreach (var test in Tests)
            {
                try
                {
                    var result = test.Run(post, Column);
                    if (result == Test.Result.ERROR)
                    {
                        ErrorCount++;
                    }
                }
                catch (Exception ex)
                {
                    if (!TestFailures.ContainsKey(test))
                    {
                        TestFailures[test] = new List<Tuple<Post, Exception>>();
                    }
                    TestFailures[test].Add(new Tuple<Post, Exception>(post, ex));
                }
            }
        }

        /// <summary>
        /// Update the length measurements given the new post.
        /// </summary>
        /// <param name="post">A post</param>
        public void UpdateLengthStatistics(Post post)
        {
            bool allPreviousNull = AllNullSoFar;

            AllNullSoFar &= post.IsNull;
            if (post.IsNull)
                return;

            var data = post.Data;

            if (TimestampFormat && (TimestampFormat &= Test.timestamp_regex.Match(data).Success))
            {
                var components = data.Split('.');
                if (components.Length == 1)
                {
                    components = new string[] { components[0], "" };
                }
                if (FirstRowAnalyzed && !allPreviousNull)
                {
                    TimestampMinParameter.Precision = (uint)Math.Min(TimestampMinParameter.Precision, components.Length == 1 ? 0 : components[1].Length);
                    TimestampMaxParameter.Precision = (uint)Math.Max(TimestampMaxParameter.Precision, components.Length == 1 ? 0 : components[1].Length);
                }
                else
                {
                    TimestampMinParameter = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                    TimestampMaxParameter = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                }
            }

            if (TimestampTimeZoneFormat && (TimestampTimeZoneFormat &= Test.timestamp_timezone_regex.Match(data).Success))
            {
                // Remove time zone info
                var components = data.Split('+');
                if (components.Length == 1)
                {
                    components = data.Split('Z');
                }

                // isolate fractional part
                components = components[0].Split('.');
                if (components.Length == 1)
                {
                    components = new string[] { components[0], "" };
                }
                if (FirstRowAnalyzed && !allPreviousNull)
                {
                    TimestampTimeZoneMinParameter.Precision = (uint)Math.Min(TimestampTimeZoneMinParameter.Precision, components.Length == 1 ? 0 : components[1].Length);
                    TimestampTimeZoneMaxParameter.Precision = (uint)Math.Max(TimestampTimeZoneMaxParameter.Precision, components.Length == 1 ? 0 : components[1].Length);
                }
                else
                {
                    TimestampTimeZoneMinParameter = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                    TimestampTimeZoneMaxParameter = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                }
            }

            if (TimeFormat && (TimeFormat &= Test.time_regex.Match(data).Success))
            {
                var components = data.Split('.');
                if (components.Length == 1)
                {
                    components = new string[] { components[0], "" };
                }
                if (FirstRowAnalyzed && !allPreviousNull)
                {
                    TimeMinParameter.Precision = (uint)Math.Min(TimeMinParameter.Precision, components.Length == 1 ? 0 : components[1].Length);
                    TimeMaxParameter.Precision = (uint)Math.Max(TimeMaxParameter.Precision, components.Length == 1 ? 0 : components[1].Length);
                }
                else
                {
                    TimeMinParameter = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                    TimeMaxParameter = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                }
            }

            if (TimeTimeZoneFormat && (TimeTimeZoneFormat &= Test.time_timezone_regex.Match(data).Success))
            {
                // Remove time zone info
                var components = data.Split('+');
                if (components.Length == 1)
                {
                    components = data.Split('Z');
                }

                // isolate fractional part
                components = components[0].Split('.');
                if (components.Length == 1)
                {
                    components = new string[] { components[0], "" };
                }
                if (FirstRowAnalyzed && !allPreviousNull)
                {
                    TimeTimeZoneMinParameter.Precision = (uint)Math.Min(TimeTimeZoneMinParameter.Precision, components.Length == 1 ? 0 : components[1].Length);
                    TimeTimeZoneMaxParameter.Precision = (uint)Math.Max(TimeTimeZoneMaxParameter.Precision, components.Length == 1 ? 0 : components[1].Length);
                }
                else
                {
                    TimeTimeZoneMinParameter = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                    TimeTimeZoneMaxParameter = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                }
            }

            if (DateFormat)
                DateFormat &= Test.date_regex.Match(data).Success;

            if (FloatFormat && (FloatFormat &= Test.float_regex.Match(data).Success))
            {
                // TODO: Interpret precision
            }

            if (NumericFormat && (NumericFormat &= Test.numeric_regex.Match(data).Success))
            {
                var components = data.Split('.');
                if (components.Length > 0 && components[0].Length > 0 && components[0][0] == '-')
                {
                    components[0] = components[0].Substring(1);
                }
                if (components.Length == 1)
                {
                    components = new string[] { components[0], "" };
                }
                if (FirstRowAnalyzed &&  !allPreviousNull)
                {
                    NumericMinParameter.Precision = (uint)Math.Min(NumericMinParameter.Precision, components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                    NumericMinParameter.Scale = (uint)Math.Min(NumericMinParameter.Scale, components.Length == 1 ? 0 : components[1].Length);
                    NumericMaxParameter.Precision = (uint)Math.Max(NumericMaxParameter.Precision, components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                    NumericMaxParameter.Scale = (uint)Math.Max(NumericMaxParameter.Scale, components.Length == 1 ? 0 : components[1].Length);
                }
                else
                {
                    NumericMinParameter = Parameter.WithPrecisionAndScale((uint)(components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length), (uint)(components.Length == 1 ? 0 : components[1].Length));
                    NumericMaxParameter = Parameter.WithPrecisionAndScale((uint)(components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length), (uint)(components.Length == 1 ? 0 : components[1].Length));
                }
            }

            if (IntegerFormat)
                IntegerFormat &= Test.integer_regex.Match(data).Success;

            // Always measure character lengths
            if (FirstRowAnalyzed && !allPreviousNull)
            {
                CharacterMinParameter.Length = (uint)Math.Min(CharacterMinParameter.Length, data.Length);
                CharacterMaxParameter.Length = (uint)Math.Max(CharacterMaxParameter.Length, data.Length);
            }
            else
            {
                CharacterMinParameter = Parameter.WithLength((uint)data.Length);
                CharacterMaxParameter = Parameter.WithLength((uint)data.Length);
            }
        }

        /// <summary>
        /// Add a suggested type to this column analysis, after analysis has been performed.
        /// </summary>
        public void SuggestType()
        {
            if (!FirstRowAnalyzed)
                return;

            if (!ForceCharacterType)
            {
                if (IntegerFormat)
                {
                    SuggestedType = new ParameterizedDataType(DataType.INTEGER, null);
                }
                else if (NumericFormat && Column.ParameterizedDataType.DataType == DataType.NUMERIC)
                {
                    SuggestedType = new ParameterizedDataType(DataType.NUMERIC, NumericMaxParameter);
                }
                else if (NumericFormat)
                {
                    SuggestedType = new ParameterizedDataType(DataType.DECIMAL, NumericMaxParameter);
                }
                else if (FloatFormat)
                {
                    SuggestedType = new ParameterizedDataType(DataType.FLOAT, Parameter.WithPrecision(18));
                }
                else if (DateFormat)
                {
                    SuggestedType = new ParameterizedDataType(DataType.DATE, null);
                }
                else if (TimestampFormat)
                {
                    SuggestedType = new ParameterizedDataType(DataType.TIMESTAMP, TimestampMaxParameter);
                }
                else if (TimestampTimeZoneFormat)
                {
                    SuggestedType = new ParameterizedDataType(DataType.TIMESTAMP_WITH_TIME_ZONE, TimestampTimeZoneMaxParameter);
                }
                else if (TimeFormat)
                {
                    SuggestedType = new ParameterizedDataType(DataType.TIME, TimeMaxParameter);
                }
                else if (TimeTimeZoneFormat)
                {
                    SuggestedType = new ParameterizedDataType(DataType.TIME_WITH_TIME_ZONE, TimeTimeZoneMaxParameter);

                }
            }

            if (SuggestedType == null && (Column.ParameterizedDataType.DataType == DataType.NATIONAL_CHARACTER || Column.ParameterizedDataType.DataType == DataType.NATIONAL_CHARACTER_VARYING))
            {
                if (CharacterMinParameter.Length == CharacterMaxParameter.Length)
                {
                    SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER, Parameter.WithLength(CharacterMaxParameter.Length));
                }
                else if (CharacterMinParameter.Length != CharacterMaxParameter.Length)
                {
                    SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER_VARYING, Parameter.WithLength(CharacterMaxParameter.Length));
                }
                return;
            }

            if (SuggestedType == null && CharacterMinParameter.Length == CharacterMaxParameter.Length)
            {
                SuggestedType = new ParameterizedDataType(DataType.CHARACTER, Parameter.WithLength(CharacterMaxParameter.Length));
            }
            else if (SuggestedType == null && CharacterMinParameter.Length != CharacterMaxParameter.Length)
            {
                SuggestedType = new ParameterizedDataType(DataType.CHARACTER_VARYING, Parameter.WithLength(CharacterMaxParameter.Length));
            }

            if (SuggestedType != null && SuggestedType.CompareTo(Column.ParameterizedDataType) == 0)
            {
                SuggestedType = null;
            }
        }

        /// <summary>
        /// Clear the column analysis, setting the error count to zero, clearing all tests and test failures.
        /// </summary>
        public void Clear()
        {
            ErrorCount = 0;
            Tests.Clear();
            TestFailures.Clear();
        }
    }
}
