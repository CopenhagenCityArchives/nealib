using NEA.ArchiveModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NEA.Helpers
{
    public delegate void FilesVerifiedEventHandler(FilesVerifiedEventArgs e);
    public class FilesVerifiedEventArgs : EventArgs
    {
        public int ProcessedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public int ErrorsCount { get; set; }
        public int AVFilesFound { get; set; }
    }
    public class MD5Helper
    {
        private readonly IFileSystem _fileSystem;
        /// <summary>
        /// Fires when for each 1% of files verified or for every file verified if less than 100 files 
        /// </summary>
        public event FilesVerifiedEventHandler FilesVerified;
                
        /// <summary>
        /// Fires when a checksum error is found
        /// </summary>
        public event EventHandler<string> VerifyFailed;

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
        public async Task<Dictionary<string, bool>> VerifyChecksumsAsync(ArchiveVersion1007 av, bool includeDocuments = true)
        {
            return await Task.Run(() =>
            {
                return VerifyChecksums(av, includeDocuments);                
            });
        }
        /// <summary>
        /// Verifies the calculated checksums of archive version files against the expected values found in its index
        /// </summary>
        /// <param name="av">The archive version to be checked</param>
        /// <param name="includeDocuments">Indicate wether documents files should also be verified</param>
        /// <returns>A dictionary of (key)filepaths and (value)verification result</returns>
        public Dictionary<string, bool> VerifyChecksums(ArchiveVersion1007 av, bool includeDocuments = true)
        {
            var avFiles = av.GetFiles();
            av.LoadFileIndex();
            var checkFiles = avFiles.MetadataData.Concat(avFiles.TableData);
            if (includeDocuments)
            {
                checkFiles = checkFiles.Concat(avFiles.Documents);
            }

            int checkedFiles = 0;
            int failedChecks = 0;
            int notifyFrequency = (int)Math.Ceiling((decimal)checkFiles.Count() / 100); //We want to notify at least for each 1% of files processed
            var resultDict = new ConcurrentDictionary<string, bool>();
            var expectedChecksums = av.GetChecksumDictString();

            Parallel.ForEach(checkFiles, new ParallelOptions { MaxDegreeOfParallelism = 1 }, file =>
            {
                //foreach (string file in checkFiles)
                //{

                    Interlocked.Increment(ref checkedFiles);

                //Console.WriteLine("checksum, file:");
                //Console.WriteLine(CalculateChecksumString(file));

                //Console.WriteLine("checksum, expected");
                //var tes = expectedChecksums.FirstOrDefault(x => x.Key == av.GetRelativeFilePath(file)).Value;
                //Console.WriteLine(expectedChecksums.FirstOrDefault(x => x.Key == av.GetRelativeFilePath(file)));

                string relativePath = av.GetRelativeFilePath(file);
                if (relativePath.IndexOf("fileIndex.xml") != -1)
                {
                    return;
                }

                var result = CalculateChecksumString(file) == expectedChecksums.FirstOrDefault(x => x.Key == av.GetRelativeFilePath(file)).Value;
                if (!resultDict.TryAdd(file, result))
                {
                    throw new InvalidOperationException($"Cannot process duplicate filepath! {file}");
                }

                if (!result)
                {
                    Interlocked.Increment(ref failedChecks);
                    OnVerifyFailed(file);
                }

                if (checkedFiles % notifyFrequency == 0)
                {
                    OnFilesVerified(new FilesVerifiedEventArgs { ProcessedFiles = checkedFiles, ErrorsCount = failedChecks });
                }
                //}
            });
            return resultDict.ToDictionary(x => x.Key, x => x.Value);
        }
        protected virtual void OnVerifyFailed(string FilePath)
        {
            EventHandler<string> handler = VerifyFailed;
            if (handler != null)
            {
                handler(this, FilePath);
            }
        }
        protected virtual void OnFilesVerified(FilesVerifiedEventArgs e)
        {
            FilesVerifiedEventHandler handler = FilesVerified;
            if (handler != null)
            {
                handler(e);
            }
        }
    }
}
