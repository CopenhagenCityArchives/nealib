using NEA.Archiving;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NEA.Utility
{
    /// <summary>
    /// Fires when a non document file or 1000 document files are processed
    /// </summary>
    public delegate void FileProcessedEventHandler(FileProcessedEventArgs e);

    /// <summary>
    /// Fires when fileIndex.xml is read
    /// </summary>
    public delegate void FileIndexReadEventHandler(FileIndexReadEventArgs e);

    /// <summary>
    /// Contains information about the files processed:
    /// Processed files
    /// Skipped files
    /// Number of errors
    /// </summary>
    public class FileProcessedEventArgs : EventArgs
    {
        public int ProcessedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public int ErrorsCount { get; set; }
    }

    /// <summary>
    /// Contains information about the FileIndex:
    /// Total number of files
    /// Number of non document files
    /// </summary>
    public class FileIndexReadEventArgs : EventArgs
    {
        public int TotalFiles { get; set; }
        public int NonDocumentFiles { get; set; }
    }

    public class FileIndexChecksumValidator
    {
        public event FileProcessedEventHandler FileProcessed;
        protected virtual void OnFileProcessed(FileProcessedEventArgs e)
        {
            FileProcessedEventHandler handler = FileProcessed;
            if (handler != null)
            {
                handler(e);
            }
        }

        public event FileIndexReadEventHandler FileIndexRead;

        protected virtual void OnFileIndexRead(FileIndexReadEventArgs e)
        {
            FileIndexReadEventHandler handler = FileIndexRead;
            if(handler != null)
            {
                handler(e);
            }
        }

        /// <summary>
        /// Fires when the validation is done
        /// </summary>
        public event FileProcessedEventHandler ValidationDone;
        
        /// <summary>
        /// Handles ValidationDone events
        /// </summary>
        protected virtual void OnValidationDone(FileProcessedEventArgs e)
        {
            FileProcessedEventHandler handler = ValidationDone;
            if (handler != null)
            {
                handler(e);
            }
        }

        /// <summary>
        /// Fires when a checksum error is found
        /// </summary>
        public event EventHandler<string> ErrorFound;

        /// <summary>
        /// Handles OnError events
        /// </summary>
        /// <param name="FilePath"></param>
        protected virtual void OnErrorFound(string FilePath)
        {
            EventHandler<string> handler = ErrorFound;
            if (handler != null)
            {
                handler(this, FilePath);
            }
        }

        private int _iteratedFiles;
        private int _checkedFiles;
        private int _skippedFiles;
        private int _errors;
        private int _nonDocumentFilesInFileIndex;

        private bool _checkDocuments;

        private string _archiveversionPath;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">The path of the archiveversion</param>
        /// <param name="validateDocuments">Wheter or not to validate document type files</param>
        public FileIndexChecksumValidator(string path, bool validateDocuments)
        {
            _archiveversionPath = path;
            _checkDocuments = validateDocuments;
        }

        public void ValidateFileIndex()
        {
            var avInfo = new ArchiveVersionInfo();
            var avIdentifier = new ArchiveVersionIdentifier();
            avIdentifier.TryGetAvFolder(out avInfo, _archiveversionPath);

            ArchiveVersion av = new ArchiveVersion(avInfo.Id, avInfo.FolderPath, null);
            av.Medias = avIdentifier.GetArciveversionMediaFolders(avInfo);

            var fr = new FileIndexReader(av);

            var avFilesConcurrent = new ConcurrentQueue<AVFile>();

            foreach (AVFile f in fr.ReadFiles())
            {
                if (f.AvFileType != AVFileType.DOCUMENT)
                {
                    Interlocked.Increment(ref _nonDocumentFilesInFileIndex);
                }
                avFilesConcurrent.Enqueue(f);
            }

            OnFileIndexRead(new FileIndexReadEventArgs { TotalFiles = avFilesConcurrent.Count, NonDocumentFiles = _nonDocumentFilesInFileIndex });

            Parallel.ForEach(avFilesConcurrent, new ParallelOptions { MaxDegreeOfParallelism = 16 }, item =>
            {
                Interlocked.Increment(ref _iteratedFiles);

                if (!_checkDocuments && item.AvFileType.Equals(AVFileType.DOCUMENT))
                {
                    Interlocked.Increment(ref _skippedFiles);
                    return;
                }

                if (!item.ValidateIndicatedChecksum(av.Path))
                {
                    Interlocked.Increment(ref _errors);
                    OnErrorFound(item.FilePath + "\\"+ item.FileName);
                }

                Interlocked.Increment(ref _checkedFiles);

                if (_checkedFiles % 1000 == 0 || !_checkDocuments)
                {
                    OnFileProcessed(new FileProcessedEventArgs { ProcessedFiles = _iteratedFiles, ErrorsCount = _errors, SkippedFiles = _skippedFiles });
                }

            });

            OnValidationDone(new FileProcessedEventArgs { ProcessedFiles = _iteratedFiles, ErrorsCount = _errors, SkippedFiles = _skippedFiles });
        }
    }
}
