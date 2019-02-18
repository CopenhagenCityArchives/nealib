using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Archiving
{
    public enum ByteOrder {
        LittleEndian,
        BigEndian
    }

    public class IFDEntry
    {
        public ushort Tag { get; private set; }
        public ushort FieldType { get; private set; }
        public uint Count { get; private set; }
        public uint ValueOffset { get; private set; }

        public IFDEntry(ushort tag, ushort fieldType, uint count, uint valueOffset)
        {
            Tag = tag;
            FieldType = fieldType;
            Count = count;
            ValueOffset = valueOffset;
        }
    }

    public class TiffMetadataReader : IDisposable
    {
        public ByteOrder ByteOrder { get; private set; }

        FileStream stream;
        uint nextIfdOffset;
        bool byteOrderMismatch;

        public TiffMetadataReader(string fileName)
        {
            stream = new FileStream(fileName, FileMode.Open);
            ReadFileHeader();
        }

        public IEnumerable<Dictionary<ushort, IFDEntry>> ReadMetadata()
        {
            while (nextIfdOffset != 0)
            {
                yield return ReadIFD();
            }
        }

        Dictionary<ushort, IFDEntry> ReadIFD()
        {
            stream.Seek(nextIfdOffset, SeekOrigin.Begin);
            var count = ReadValue(2, BitConverter.ToUInt16);
            var dict = new Dictionary<ushort, IFDEntry>();

            while (count > 0)
            {
                var entry = ReadIFDEntry();
                dict[entry.Tag] = entry;
                count--;
            }

            nextIfdOffset = ReadValue(4, BitConverter.ToUInt32);
            return dict;
        }

        IFDEntry ReadIFDEntry()
        {
            ushort tag, fieldType;
            uint count, valueOffset;

            tag = ReadValue(2, BitConverter.ToUInt16);
            fieldType = ReadValue(2, BitConverter.ToUInt16);
            count = ReadValue(4, BitConverter.ToUInt32);
            valueOffset = ReadValue(4, BitConverter.ToUInt32);

            return new IFDEntry(tag, fieldType, count, valueOffset);
        }

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
            nextIfdOffset = ReadValue(4, BitConverter.ToUInt32);
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


        void HandleByteOrder(byte[] buffer)
        {
            if (byteOrderMismatch)
                Array.Reverse(buffer);
        }

        public void Dispose()
        {
            stream.Dispose();
        }
    }
}
