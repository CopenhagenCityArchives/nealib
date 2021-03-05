using System;
using System.Collections.Generic;
using System.Text;

namespace NEA.Helpers
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
            long absolute_bytes = bytes < 0 ? -bytes : bytes;
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_bytes >= ExaByte)
            {
                suffix = "EB";
                readable = bytes >> 50;
            }
            else if (absolute_bytes >= PetaByte)
            {
                suffix = "PB";
                readable = bytes >> 40;
            }
            else if (absolute_bytes >= TeraByte)
            {
                suffix = "TB";
                readable = bytes >> 30;
            }
            else if (absolute_bytes >= GigaByte)
            {
                suffix = "GB";
                readable = bytes >> 20;
            }
            else if (absolute_bytes >= MegaByte)
            {
                suffix = "MB";
                readable = bytes >> 10;
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
            readable = readable / 1024;
            // Return formatted number with suffix
            return readable.ToString("0.## ") + suffix;
        }
        #region Hex string parser
        //The below code is shamelessly stolen from a mad stack overflow discussion where it was proclaimed the "most eficient" way of connverting a hex string back into a byte array.
        //I dont know if it truly is the most efficient, but it was written by Jon Skeet and im not gunna pretend to second guess the patron saint of C#.
        //Original discusion here: https://stackoverflow.com/a/14332574

        public static byte[] ParseHex(string hexString)
        {
            if ((hexString.Length & 1) != 0)
            {
                throw new ArgumentException("Input must have even number of characters");
            }
            int length = hexString.Length / 2;
            byte[] ret = new byte[length];
            for (int i = 0, j = 0; i < length; i++)
            {
                int high = ParseNybble(hexString[j++]);
                int low = ParseNybble(hexString[j++]);
                ret[i] = (byte)((high << 4) | low);
            }

            return ret;
        }
        private static int ParseNybble(char c)
        {
            switch (c)
            {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return c - '0';
                case 'a':
                case 'b':
                case 'c':
                case 'd':
                case 'e':
                case 'f':
                    return c - ('a' - 10);
                case 'A':
                case 'B':
                case 'C':
                case 'D':
                case 'E':
                case 'F':
                    return c - ('A' - 10);
                default:
                    throw new ArgumentException("Invalid nybble: " + c);
            }
            return c;
        }
        #endregion
    }
}
