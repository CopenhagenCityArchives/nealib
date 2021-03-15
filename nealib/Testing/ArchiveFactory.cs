using NEA.Helpers;

namespace NEA.Testing
{
    public interface IArchiveFactory
    {
        IArchiveFactory AddDocCollection(int numberOfFiles, int fileSize = 1048576);
        IArchiveFactory AddTable(int fileSize = 1048576);
        IArchiveFactory AddTables(int amount, int fileSize = (int)ByteHelper.MegaByte);
    }
}