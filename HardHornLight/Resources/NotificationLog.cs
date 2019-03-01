using HardHorn.Analysis;
using HardHorn.Archiving;
using HardHorn.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HardHorn.Resources
{
    public partial class NotificationLog
    {
        ArchiveVersion ArchiveVersion;
        bool GroupByTables;
        IEnumerable<CollectionViewGroup> NotificationGroups;
        IDictionary<INotification, IEnumerable<Post>> AnalysisErrorSamples;
        IDictionary<ForeignKey, IEnumerable<Tuple<ForeignKeyValue, int>>> ForeignKeyErrorSamples;

        public NotificationLog(ArchiveVersion archiveVersion,
            IDictionary<INotification, IEnumerable<Post>> analysisErrorSamples,
            IDictionary<ForeignKey, IEnumerable<Tuple<ForeignKeyValue, int>>> foreignKeyErrorSamples,
            IEnumerable<CollectionViewGroup> notificationGroups,
            bool groupByTables)
        {
            GroupByTables = groupByTables;
            ArchiveVersion = archiveVersion;
            NotificationGroups = notificationGroups;
            ForeignKeyErrorSamples = foreignKeyErrorSamples;
            AnalysisErrorSamples = analysisErrorSamples;
        }
    }
}
