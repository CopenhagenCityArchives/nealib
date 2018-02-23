using System.Collections.Generic;

using HardHorn.Archiving;

namespace HardHorn.Utility
{
    public class ForeignKeyComparison
    {
        public string Name { get; set; }

        public ForeignKey OldForeignKey { get; set; }
        public ForeignKey NewForeignKey { get; set; }
        public bool Added { get; set; }
        public bool Removed { get; set; }
        public bool Modified { get; set; }

        public List<ReferenceComparison> References { get; set; }
        public bool ReferencesModified { get; set; }

        public ForeignKeyComparison(ForeignKey newForeignKey, ForeignKey oldForeignKey)
        {
            OldForeignKey = oldForeignKey;
            NewForeignKey = newForeignKey;
            References = new List<ReferenceComparison>();

            Added = false;
            Removed = false;
            Modified = false;
            ReferencesModified = false;
        }
    }
}
