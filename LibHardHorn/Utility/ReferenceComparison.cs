using HardHorn.Archiving;
namespace HardHorn.Utility
{
    public class ReferenceComparison
    {
        public bool Added { get; set; }
        public bool Removed { get; set; }
        public bool Modified { get; set; }

        public Reference OldReference { get; set; }
        public Reference NewReference { get; set; }

        public bool ReferencedColumnModified { get; set; }

        public string ColumnName { get; set; }

        public ReferenceComparison(Reference newReference, Reference oldReference)
        {
            NewReference = newReference;
            OldReference = oldReference;
            ReferencedColumnModified = false;
            Added = false;
            Removed = false;
            Modified = false;
        }
    }
}
