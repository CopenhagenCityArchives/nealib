using System;
using System.Collections.Generic;
using System.Text;

namespace NEA.src.Utility
{
    public static class ByteHelper
    {
        public const long KiloByte = 0x400;
        public const long MegaByte = 0x100000;
        public const long GigaByte = 0x40000000;
        public const long TeraByte = 0x10000000000;
        public const long PetaByte = 0x4000000000000;
        public const long ExaByte = 0x1000000000000000;

        /// <summary>
        /// Converts a filesize in bytes to a human readable format
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>Human readable filesize string</returns>
        public static string GetBytesReadable(long bytes)
        {
            // Get absolute value
            long absolute_bytes = (bytes < 0 ? -bytes : bytes);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_bytes >= ExaByte)
            {
                suffix = "EB";
                readable = (bytes >> 50);
            }
            else if (absolute_bytes >= PetaByte)
            {
                suffix = "PB";
                readable = (bytes >> 40);
            }
            else if (absolute_bytes >= TeraByte)
            {
                suffix = "TB";
                readable = (bytes >> 30);
            }
            else if (absolute_bytes >= GigaByte)
            {
                suffix = "GB";
                readable = (bytes >> 20);
            }
            else if (absolute_bytes >= MegaByte)
            {
                suffix = "MB";
                readable = (bytes >> 10);
            }
            else if (absolute_bytes >= KiloByte)
            {
                suffix = "KB";
                readable = bytes;
            }
            else
            {
                return bytes.ToString("0 B");
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix;
        }
    }
}
