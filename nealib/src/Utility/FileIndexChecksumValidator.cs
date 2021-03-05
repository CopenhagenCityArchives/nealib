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
        public int NonDocumentTypeFiles { get; set; }
    }

    public class FileIndexChecksumValidator
    {
        /// <summary>
        /// Fires when a file is processed
        /// </summary>
        public event FileProcessedEventHandler FileProcessed;

        /// <summary>
        /// Handles event handlers when FileProcessed is fired
        /// </summary>
        protected virtual void OnFileProcessed(FileProcessedEventArgs e)
        {
            FileProcessedEventHandler handler = FileProcessed;
            if (handler != null)
            {
                handler(e);
            }
        }

        /// <summary>
        /// Fires when fileIndex.xml is read
        /// </summary>
        public event FileIndexReadEventHandler FileIndexRead;

        /// <summary>
        /// Handles event handlers wen FileIndexRead is fired
        /// </summary>
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
        /// Handles event handlers wen ValidationDone is fired
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

        /// <summary>
        /// Number of files iterated in validation
        /// </summary>
        private int _iteratedFiles;
        
        /// <summary>
        /// Number of skipped files in validation
        /// </summary>
        private int _skippedFiles;
        
        /// <summary>
        /// Number of errors in validation
        /// </summary>
        private int _errors;
        
        /// <summary>
        /// Number of files not being of type AVFileType.Document
        /// </summary>
        private int _nonDocumentTypeFilesInFileIndex;

        /// <summary>
        /// Whether or not to check files of type AVFileType.Document
        /// </summary>
        private bool _checkDocuments;

        /// <summary>
        /// Path to archiveversion
        /// </summary>
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

        /// <summary>
        /// Validates files in a fileIndex.xml in multiple threads. Validates documents of AVFileType.Document if set in constructor
        /// </summary>
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
                    Interlocked.Increment(ref _nonDocumentTypeFilesInFileIndex);
                }
                avFilesConcurrent.Enqueue(f);
            }

            OnFileIndexRead(new FileIndexReadEventArgs { TotalFiles = avFilesConcurrent.Count, NonDocumentTypeFiles = _nonDocumentTypeFilesInFileIndex });

            Parallel.ForEach(avFilesConcurrent, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, item =>
            {
                Interlocked.Increment(ref _iteratedFiles);

                if (_iteratedFiles % 1000 == 0 || (!_checkDocuments && !item.AvFileType.Equals(AVFileType.DOCUMENT)))
                {
                    OnFileProcessed(new FileProcessedEventArgs { ProcessedFiles = _iteratedFiles, ErrorsCount = _errors, SkippedFiles = _skippedFiles });
                }

                if (!_checkDocuments && item.AvFileType.Equals(AVFileType.DOCUMENT))
                {
                    Interlocked.Increment(ref _skippedFiles);
                }
                else
                {
                    if (!item.ValidateIndicatedChecksum(av.Path))
                    {
                        Interlocked.Increment(ref _errors);
                        OnErrorFound(item.FilePath + "\\" + item.FileName);
                    }
                }
            });

            OnValidationDone(new FileProcessedEventArgs { ProcessedFiles = _iteratedFiles, ErrorsCount = _errors, SkippedFiles = _skippedFiles });
        }
    }
}
