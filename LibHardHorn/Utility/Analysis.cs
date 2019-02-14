using HardHorn.Analysis;

namespace HardHorn.Utility
{
    public static class AnalysisUtility
    {
        public static string AnalysisTestTypeToString(AnalysisTestType testType)
        {
            switch (testType)
            {
                case AnalysisTestType.UNDERFLOW:
                    return "Underudfyldelse";
                case AnalysisTestType.OVERFLOW:
                    return "Overskridelse";
                case AnalysisTestType.BLANK:
                    return "Foran- eller efterstillede blanktegn";
                case AnalysisTestType.FORMAT:
                    return "Formateringsfejl";
                case AnalysisTestType.REGEX:
                    return "Match af regulært udtryk";
                case AnalysisTestType.UNALLOWED_KEYWORD:
                    return "Ulovlige keywords";
                case AnalysisTestType.REPEATING_CHAR:
                    return "Gentagne karakterer";
                default:
                    return string.Empty;
            }
        }
    }
}
