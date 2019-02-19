using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Archiving
{
    /// <summary>
    /// An enumeration of possible tiff byte orders.
    /// </summary>
    public enum ByteOrder {
        LittleEndian,
        BigEndian
    }

    /// <summary>
    /// An enumeration of possible tiff field types.
    /// </summary>
    public enum TiffType : ushort
    {
        BYTE = 1,
        ASCII = 2,
        SHORT = 3,
        LONG = 4,
        RATIONAL = 5,
        SBYTE = 6,
        UNDEFINED = 7,
        SSHORT = 8,
        SLONG = 9,
        SRATIONAL = 10,
        FLOAT = 11,
        DOUBLE = 12
    }

    /// <summary>
    /// A directory containing information about an image file in the Tiff file.
    /// </summary>
    public class ImageFileDirectory
    {
        /// <summary>
        /// The offset of this image file directory.
        /// </summary>
        public uint Offset { get; private set; }

        /// <summary>
        /// The number of entries in this directory.
        /// </summary>
        public uint Count { get; private set; }

        /// <summary>
        /// The entries in this directory.
        /// </summary>
        public IReadOnlyList<ImageFileDirectoryEntry> Entries { get; private set; }

        /// <summary>
        /// The offset to the next image file directory, if there is one.
        /// </summary>
        public uint? NextImageFileDirectoryOffset { get; private set; }

        /// <summary>
        /// True, if this is the last image file directory, false if it is not.
        /// </summary>
        public bool LastImageFileDirectory { get { return !NextImageFileDirectoryOffset.HasValue; } }

        /// <summary>
        /// Construct an image file directory object.
        /// </summary>
        /// <param name="count">The number of entries.</param>
        /// <param name="entries">The actual image file directory entries.</param>
        /// <param name="nextIfdOffset">The offset value to the next image file directory. Regarded as a missing value if 0.</param>
        public ImageFileDirectory(uint offset, uint count, IEnumerable<ImageFileDirectoryEntry> entries, uint nextIfdOffset)
        {
            Count = count;
            Entries = new List<ImageFileDirectoryEntry>(entries);
            if (nextIfdOffset == 0)
                NextImageFileDirectoryOffset = null;
            else
                NextImageFileDirectoryOffset = nextIfdOffset;
        }

        public override string ToString()
        {
            return $"<ImageFileDirectory Offset={Offset.ToString("X")} Count={Count}>";
        }
    }

    /// <summary>
    /// An entry into an ImageFileDirectory.
    /// </summary>
    public class ImageFileDirectoryEntry
    {
        /// <summary>
        /// The Tiff Tag of this entry, describing the kind of data in the entry.
        /// </summary>
        public TiffTag Tag { get; private set; }

        /// <summary>
        /// The type of the data in the field.
        /// </summary>
        public TiffType FieldType { get; private set; }

        /// <summary>
        /// The number of values in the field.
        /// </summary>
        public uint Count { get; private set; }

        byte[] Value;

        public ImageFileDirectoryEntry(ushort tag, ushort fieldType, uint count, byte[] value)
        {
            Count = count;
            Value = value;

            if (TiffTag.TiffTags.ContainsKey(tag))
                Tag = TiffTag.TiffTags[tag];

            if (Enum.IsDefined(typeof(TiffType), fieldType))
                FieldType = (TiffType)fieldType;
        }

        /// <summary>
        /// Retrieves the value of this entry. If it is a referenced value, the reference will be followed and read.
        /// </summary>
        /// <param name="stream">The Tiff file stream.</param>
        /// <returns></returns>
        public object GetValue(Stream stream)
        {
            if (IsValueReference())
            {
                uint offset = BitConverter.ToUInt32(Value, 0);
                stream.Seek(offset, SeekOrigin.Begin);
                return null;
            }
            else
            {
                switch (FieldType)
                {
                    case TiffType.BYTE:
                        return Value[0];
                    case TiffType.ASCII:
                        return (char)Value[0];
                    case TiffType.SBYTE:
                        return (sbyte)Value[0];
                    case TiffType.UNDEFINED:
                        return (object)Value[0];
                    case TiffType.SHORT:
                        return BitConverter.ToUInt16(Value, 0);
                    case TiffType.SSHORT:
                        return BitConverter.ToInt16(Value, 0);
                    case TiffType.LONG:
                        return BitConverter.ToUInt32(Value, 0);
                    case TiffType.SLONG:
                        return BitConverter.ToInt32(Value, 0);
                    case TiffType.FLOAT:
                        return BitConverter.ToSingle(Value, 0);
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        /// <summary>
        /// Check if the the value in the IFD entry is a reference, or the actual value.
        /// </summary>
        /// <returns>'True', if the </returns>
        public bool IsValueReference()
        {
            uint size;

            switch (FieldType)
            {
                case TiffType.BYTE:
                case TiffType.ASCII:
                case TiffType.SBYTE:
                case TiffType.UNDEFINED:
                    size = 1;
                    break;
                case TiffType.SHORT:
                case TiffType.SSHORT:
                    size = 2;
                    break;
                case TiffType.LONG:
                case TiffType.SLONG:
                case TiffType.FLOAT:
                    size = 4;
                    break;
                case TiffType.RATIONAL:
                case TiffType.SRATIONAL:
                case TiffType.DOUBLE:
                    size = 8;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return size * Count > 4;
        }

        public override string ToString()
        {
            return $"<{FieldType.ToString()} Tag={Tag} Count={Count}>";
        }
    }

    /// <summary>
    /// A Tiff file.
    /// </summary>
    public class Tiff : IDisposable
    {
        /// <summary>
        /// The order (endian-ness) of the bytes.
        /// </summary>
        public ByteOrder ByteOrder { get; private set; }

        /// <summary>
        /// The image file directories contained in the Tiff file.
        /// </summary>
        public IReadOnlyList<ImageFileDirectory> ImageFileDirectories { get { return imageFileDirectories; } }

        /// <summary>
        /// The offset to the first image file directory in the file.
        /// </summary>
        public uint FirstImageFileDirectoryOffset { get; private set; }

        FileStream stream;
        bool byteOrderMismatch;
        List<ImageFileDirectory> imageFileDirectories = new List<ImageFileDirectory>();

        /// <summary>
        /// Prevent construction.
        /// </summary>
        private Tiff() { }

        /// <summary>
        /// Open a Tiff file.
        /// </summary>
        /// <param name="path">The path to the Tiff file.</param>
        /// <returns></returns>
        public static Tiff Open(string path)
        {
            var tiff = new Tiff();
            tiff.stream = new FileStream(path, FileMode.Open);
            tiff.ReadFileHeader();
            return tiff;
        }

        /// <summary>
        /// Read the image file directory at the given <paramref name="offset"/>, and add it to the collection of directories in the file.
        /// </summary>
        /// <param name="offset">The offset of the image file directory.</param>
        /// <returns>The image file directory.</returns>
        public ImageFileDirectory ReadImageFileDirectory(uint offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            var count = ReadValue(2, BitConverter.ToUInt16);
            var entries = new List<ImageFileDirectoryEntry>();

            for (int i = 0; i < count; i++)
            {
                var entry = ReadImageFileDirectoryEntry();
                entries.Add(entry);

            }
            var nextIfdOffset = ReadValue(4, BitConverter.ToUInt32);
            var ifd = new ImageFileDirectory(offset, count, entries, nextIfdOffset);

            imageFileDirectories.Add(ifd);
            return ifd;
        }

        /// <summary>
        /// Read an image file directory entry.
        /// </summary>
        /// <returns>The read image file directory entry.</returns>
        ImageFileDirectoryEntry ReadImageFileDirectoryEntry()
        {
            ushort tag, fieldType;
            uint count;
            byte[] value = new byte[4];

            tag = ReadValue(2, BitConverter.ToUInt16);
            fieldType = ReadValue(2, BitConverter.ToUInt16);
            count = ReadValue(4, BitConverter.ToUInt32);
            if (stream.Read(value, 0, 4) != 4)
                throw new InvalidOperationException();

            return new ImageFileDirectoryEntry(tag, fieldType, count, value);
        }

        /// <summary>
        /// Reads and parses the Tiff file header, setting byte order value, testing magic bytes
        /// and finding the offset of the first image file directory.
        /// </summary>
        void ReadFileHeader()
        {
            byte[] buffer;

            // Get endian-ness
            buffer = new byte[2];
            if (stream.Read(buffer, 0, 2) != 2)
                throw new InvalidOperationException();
            if (buffer[0] != buffer[1])
                throw new InvalidOperationException();
            if (buffer[0] == 0x49)
                ByteOrder = ByteOrder.LittleEndian;
            else if (buffer[0] == 0x4d)
                ByteOrder = ByteOrder.BigEndian;
            else
                throw new InvalidOperationException();
            byteOrderMismatch = BitConverter.IsLittleEndian && ByteOrder == ByteOrder.BigEndian || !BitConverter.IsLittleEndian && ByteOrder != ByteOrder.BigEndian;

            // Test magic bytes
            if (ReadValue(2, BitConverter.ToInt16) != 42)
                throw new InvalidOperationException();

            // Get next IFD offset
            FirstImageFileDirectoryOffset = ReadValue(4, BitConverter.ToUInt32);
        }

        /// <summary>
        /// Reads 'count' bytes from the stream, and converts them with the given
        /// converter function.
        /// </summary>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The converted value.</returns>
        T ReadValue<T>(int count, Func<byte[], int, T> converter)
        {
            byte[] buffer = new byte[count];
            if (stream.Read(buffer, 0, count) != count)
                throw new InvalidOperationException();
            HandleByteOrder(buffer);
            return converter(buffer, 0);
        }

        /// <summary>
        /// If the endian-ness of the system and the file do not match, reverse the byte buffer.
        /// </summary>
        /// <param name="buffer">The buffer whose bytes will be handled.</param>
        void HandleByteOrder(byte[] buffer)
        {
            if (byteOrderMismatch)
                Array.Reverse(buffer);
        }

        /// <summary>
        /// Release all resources used by the ``
        /// </summary>
        public void Dispose()
        {
            stream.Dispose();
        }
    }
}
