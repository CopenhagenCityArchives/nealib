using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Analysis
{
    public abstract class AnalysisErrorsOccuredBase
    {
        public event AnalysisErrorOccuredEventHandler AnalysisErrorsOccured;
        public delegate void AnalysisErrorOccuredEventHandler(object sender, AnalysisErrorsOccuredArgs e);
        protected virtual void NotifyOfAnalysisErrorOccured(Test test, IEnumerable<Post> posts, Column column)
        {
            if (AnalysisErrorsOccured != null)
                AnalysisErrorsOccured(this, new AnalysisErrorsOccuredArgs(test, posts, column));
        }
    }

    public class AnalysisErrorsOccuredArgs : EventArgs
    {
        public AnalysisErrorsOccuredArgs(Test test, IEnumerable<Post> posts, Column column)
        {
            Column = column;
            Posts = posts;
            Test = test;
        }

        public Test Test { get; set; }
        public Test.Result Result { get; set; }
        public Column Column { get; set; }
        public IEnumerable<Post> Posts { get; set; }
    }
}
