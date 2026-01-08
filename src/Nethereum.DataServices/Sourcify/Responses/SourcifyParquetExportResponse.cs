using System;
using System.Collections.Generic;

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyParquetListResponse
    {
        public string Name { get; set; }
        public string Prefix { get; set; }
        public string Marker { get; set; }
        public string NextMarker { get; set; }
        public bool IsTruncated { get; set; }
        public List<SourcifyParquetFileInfo> Contents { get; set; } = new List<SourcifyParquetFileInfo>();
    }

    public class SourcifyParquetFileInfo
    {
        public string Key { get; set; }
        public DateTime LastModified { get; set; }
        public string ETag { get; set; }
        public long Size { get; set; }

        public string FileName => Key?.Substring(Key.LastIndexOf('/') + 1);

        public string TableName
        {
            get
            {
                if (string.IsNullOrEmpty(Key)) return null;
                var parts = Key.Split('/');
                return parts.Length >= 2 ? parts[1] : null;
            }
        }
    }

    public class SourcifyParquetSyncResult
    {
        public int FilesDownloaded { get; set; }
        public int FilesSkipped { get; set; }
        public long BytesDownloaded { get; set; }
        public List<string> DownloadedFiles { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class SourcifyParquetSyncProgress
    {
        public string CurrentFile { get; set; }
        public int FilesProcessed { get; set; }
        public int TotalFiles { get; set; }
        public double PercentComplete => TotalFiles > 0 ? (double)FilesProcessed / TotalFiles * 100 : 0;
    }
}
