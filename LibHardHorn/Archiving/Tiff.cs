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
        public IReadOnlyDictionary<ushort, ImageFileDirectoryEntry> Entries { get; private set; }

        /// <summary>
        /// The offset to the next image file directory, if there is one.
        /// </summary>
        public uint? NextImageFileDirectoryOffset { get; private set; }

        /// <summary>
        /// True, if this is the last image file directory, false if it is not.
        /// </summary>
        public bool IsLastImageFileDirectory { get { return !NextImageFileDirectoryOffset.HasValue; } }

        /// <summary>
        /// Construct an image file directory object.
        /// </summary>
        /// <param name="count">The number of entries.</param>
        /// <param name="entries">The actual image file directory entries.</param>
        /// <param name="nextIfdOffset">The offset value to the next image file directory. Regarded as a missing value if 0.</param>
        public ImageFileDirectory(uint offset, uint count, IEnumerable<ImageFileDirectoryEntry> entries, uint nextIfdOffset)
        {
            Count = count;
            Entries = entries.ToDictionary(entry => entry.Tag.Code);
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
        /// The type of the data in the field, if it is a valid type. Otherwise no value.
        /// </summary>
        public TiffType? FieldType { get; private set; }

        /// <summary>
        /// The number of values in the field.
        /// </summary>
        public uint Count { get; private set; }

        /// <summary>
        /// Contains the value of the entry if and only if it is contained directly in
        /// the entry. Otherwise it is null.
        /// </summary>
        public object Value { get; private set; }

        /// <summary>
        /// Has a value that is the offset in the file of the value if and only if it is
        /// a reference. Otherwise it does not have a value.
        /// </summary>
        public uint? Offset { get; private set; }

        /// <summary>
        /// Construct an image file directory entry.
        /// </summary>
        /// <param name="tag">The code value of the tag.</param>
        /// <param name="fieldType">The code value of the field type.</param>
        /// <param name="count">The amount of values related to this entry.</param>
        /// <param name="value">The value or offset to the value of this entry.</param>
        public ImageFileDirectoryEntry(ushort tag, ushort fieldType, uint count, byte[] value)
        {
            Count = count;
            Value = value;

            if (TiffTag.TiffTags.ContainsKey(tag))
                Tag = TiffTag.TiffTags[tag];
            else
                Tag = new TiffTag(TiffTagNamespace.Unknown, tag, "Unknown tag", string.Empty);

            if (Enum.IsDefined(typeof(TiffType), fieldType))
                FieldType = (TiffType)fieldType;

            if (IsValueReference())
                ReadOffset(value);
            else
                ReadValueDirectly(value);
        }

        /// <summary>
        /// Check if the the value in the IFD entry is a reference, or the actual value.
        /// </summary>
        /// <returns>'True', if the </returns>
        /// <exception cref="System.InvalidOperationException">If the field type is not set.</exception>
        public bool IsValueReference()
        {
            return SizeOf() > 4;
        }

        /// <summary>
        /// Get the size in bytes of the entry value.
        /// </summary>
        /// <returns>The size in bytes of the entry value.</returns>
        /// <exception cref="System.InvalidOperationException">If the field type is not set.</exception>
        public uint SizeOf()
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

            return size * Count;
        }

        /// <summary>
        /// Reads the value directly.
        /// </summary>
        /// <param name="value"></param>
        void ReadValueDirectly(byte[] value)
        {
            switch (FieldType)
            {
                case TiffType.BYTE:
                    Value = value[0];
                    break;
                case TiffType.ASCII:
                    Value = (char)value[0];
                    break;
                case TiffType.SBYTE:
                    Value = (sbyte)value[0];
                    break;
                case TiffType.UNDEFINED:
                    Value = value[0];
                    break;
                case TiffType.SHORT:
                    Value = BitConverter.ToUInt16(value, 0);
                    break;
                case TiffType.SSHORT:
                    Value = BitConverter.ToInt16(value, 0);
                    break;
                case TiffType.LONG:
                    Value = BitConverter.ToUInt32(value, 0);
                    break;
                case TiffType.SLONG:
                    Value = BitConverter.ToInt32(value, 0);
                    break;
                case TiffType.FLOAT:
                    Value = BitConverter.ToSingle(value, 0);
                    break;
                default:
                    Value = null;
                    break;
            }
        }

        void ReadOffset(byte[] value)
        {
            Offset = BitConverter.ToUInt32(value, 0);
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
        /// Read the image file directory at the given <paramref name="offset"/>.
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

            return ifd;
        }

        /// <summary>
        /// Read the next image file directory if there is one, and add it to the internal collection.
        /// </summary>
        /// <returns>The next image file directory if there is one, or null.</returns>
        public ImageFileDirectory ReadNextImageFileDirectory()
        {
            ImageFileDirectory ifd;
            if (ImageFileDirectories.Count == 0)
            {
                ifd = ReadImageFileDirectory(FirstImageFileDirectoryOffset);
            }
            else if (!imageFileDirectories.Last().IsLastImageFileDirectory)
            {
                ifd = ReadImageFileDirectory(imageFileDirectories.Last().NextImageFileDirectoryOffset.Value);
            }
            else
            {
                return null;
            }

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
        /// Retrieves the value of this entry. If it is a referenced value, the reference will be followed and read.
        /// </summary>
        /// <param name="stream">The Tiff file stream.</param>
        /// <returns></returns>
        public object ReadImageFileDirectoryEntryReferencedValue(ImageFileDirectoryEntry entry)
        {
            if (entry.Offset.HasValue && entry.FieldType.HasValue)
            {
                stream.Seek(entry.Offset.Value, SeekOrigin.Begin);
                switch (entry.FieldType)
                {
                    case TiffType.ASCII:
                        return ReadValue((int)entry.SizeOf(), (byte[] buf) =>
                        {
                            List<string> res = new List<string>();
                            var builder = new StringBuilder();
                            for (int i = 0; i < buf.Length; i++)
                            {
                                if (buf[i] == 0)
                                {
                                    res.Add(builder.ToString());
                                    if (i < buf.Length)
                                        builder = new StringBuilder();
                                }
                                else
                                    builder.Append((char)buf[i]);
                            }
                            return res.ToArray();

                        });
                    case TiffType.BYTE:
                        return ReadValue((int)entry.SizeOf(), (byte[] buf) => buf);
                    case TiffType.DOUBLE:
                        return ReadValues(8, entry.Count, BitConverter.ToDouble);
                    case TiffType.FLOAT:
                        return ReadValues(4, entry.Count, BitConverter.ToSingle);
                    case TiffType.LONG:
                        return ReadValues(4, entry.Count, BitConverter.ToUInt32);
                    case TiffType.RATIONAL:
                        return ReadValues(8, entry.Count, (byte[] buf, int index) =>
                        {
                            uint[] rational = new uint[2];
                            rational[0] = BitConverter.ToUInt32(buf, index);
                            rational[1] = BitConverter.ToUInt32(buf, index + 4);
                            return rational;
                        });
                    case TiffType.SBYTE:
                        return ReadValues(1, entry.Count, (byte[] buf, int index) => (sbyte)buf[index]);
                    case TiffType.SHORT:
                        return ReadValues(2, entry.Count, BitConverter.ToUInt16);
                    case TiffType.SLONG:
                        return ReadValues(4, entry.Count, BitConverter.ToInt32);
                    case TiffType.SRATIONAL:
                        return ReadValues(8, entry.Count, (byte[] buf, int index) =>
                        {
                            int[] rational = new int[2];
                            rational[0] = BitConverter.ToInt32(buf, index);
                            rational[1] = BitConverter.ToInt32(buf, index + 4);
                            return rational;
                        });
                    case TiffType.SSHORT:
                        return ReadValues(2, entry.Count, BitConverter.ToInt16);
                    case TiffType.UNDEFINED:
                        return ReadValue((int)entry.SizeOf(), (byte[] buf) => buf);
                    default:
                        return null;
                }
            }

            return null;
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
        /// Reads 'count' bytes from the stream, and converts them with the given
        /// converter function.
        /// </summary>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The converted value.</returns>
        T ReadValue<T>(int count, Func<byte[], T> converter)
        {
            byte[] buffer = new byte[count];
            if (stream.Read(buffer, 0, count) != count)
                throw new InvalidOperationException();
            HandleByteOrder(buffer);
            return converter(buffer);
        }

        /// <summary>
        /// Reads <paramref name="count"/> values of size <paramref name="size"/> from the stream, and
        /// convert them with <paramref name="converter"/>.
        /// </summary>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The converted value.</returns>
        T[] ReadValues<T>(uint size, uint count, Func<byte[], int, T> converter)
        {
            T[] res = new T[count];
            byte[] buffer = new byte[count * size];
            if (stream.Read(buffer, 0, (int)(count * size)) != count * size)
                throw new InvalidOperationException();
            for (uint i = 0; i < count; i++)
            {
                HandleByteOrder(buffer, (int)(i * size), (int)size);
                res[i] = converter(buffer, (int)(i * size));
            }
            return res;
        }

        /// <summary>
        /// If the endian-ness of the system and the file do not match, reverse the array <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer">The buffer whose bytes will be handled.</param>
        void HandleByteOrder(byte[] buffer)
        {
            if (byteOrderMismatch)
                Array.Reverse(buffer);
        }

        /// <summary>
        /// If the endian-ness of the system and the file do not match, reverse the sub-array
        /// of <paramref name="buffer"/> starting at <paramref name="index"/> with length <paramref name="length"/>
        /// </summary>
        /// <param name="buffer">The array of bytes.</param>
        /// <param name="index">The start index of the sub-array.</param>
        /// <param name="length">The length of sub-array.</param>
        void HandleByteOrder(byte[] buffer, int index, int length)
        {
            if (byteOrderMismatch)
                Array.Reverse(buffer, index, length);
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
