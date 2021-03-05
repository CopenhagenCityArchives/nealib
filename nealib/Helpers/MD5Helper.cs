using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NEA.Helpers
{
    public class MD5Helper
    {
        protected readonly IFileSystem _fileSystem;

        public MD5Helper(IFileSystem fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
        }
        public byte[] CalculateChecksum(string filepath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = _fileSystem.File.OpenRead(filepath))
                {
                    return md5.ComputeHash(stream);
                }
            }
        }
        public string CalculateChecksumString(string filepath)
        {
            return BitConverter.ToString(CalculateChecksum(filepath)).Replace("-", "");
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
