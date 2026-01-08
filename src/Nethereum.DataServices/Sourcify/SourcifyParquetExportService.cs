using Nethereum.DataServices.Sourcify.Responses;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Nethereum.DataServices.Sourcify
{
    public class SourcifyParquetExportService
    {
        public const string BaseUrl = "https://export.sourcify.dev/";
        public const string DefaultPrefix = "v2/";

        public static readonly string[] AvailableTables = new[]
        {
            "sourcify_matches",
            "verified_contracts",
            "sources",
            "compiled_contracts_sources",
            "compiled_contracts",
            "contract_deployments",
            "contracts",
            "code",
            "compiled_contracts_signatures",
            "signatures"
        };

        private readonly HttpClient _httpClient;

        public SourcifyParquetExportService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public SourcifyParquetExportService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<SourcifyParquetListResponse> ListFilesAsync(
            string prefix = DefaultPrefix,
            string marker = null,
            int? maxKeys = null,
            CancellationToken cancellationToken = default)
        {
            var url = BuildListUrl(prefix, marker, maxKeys);
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var xmlContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ParseListResponse(xmlContent);
        }

        public async Task<List<SourcifyParquetFileInfo>> ListAllFilesAsync(
            string prefix = DefaultPrefix,
            CancellationToken cancellationToken = default)
        {
            var allFiles = new List<SourcifyParquetFileInfo>();
            string marker = null;

            do
            {
                var response = await ListFilesAsync(prefix, marker, null, cancellationToken).ConfigureAwait(false);
                allFiles.AddRange(response.Contents);

                if (response.IsTruncated && response.Contents.Count > 0)
                {
                    marker = response.NextMarker ?? response.Contents.Last().Key;
                }
                else
                {
                    break;
                }
            } while (true);

            return allFiles;
        }

        public async Task<List<SourcifyParquetFileInfo>> ListTableFilesAsync(
            string tableName,
            CancellationToken cancellationToken = default)
        {
            return await ListAllFilesAsync($"{DefaultPrefix}{tableName}/", cancellationToken).ConfigureAwait(false);
        }

        public async Task<Stream> DownloadFileAsync(string key, CancellationToken cancellationToken = default)
        {
            var url = $"{BaseUrl}{key}";
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }

        public async Task DownloadFileToPathAsync(
            string key,
            string localPath,
            CancellationToken cancellationToken = default)
        {
            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream = await DownloadFileAsync(key, cancellationToken).ConfigureAwait(false))
            using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await stream.CopyToAsync(fileStream).ConfigureAwait(false);
            }
        }

        public async Task<SourcifyParquetSyncResult> SyncToDirectoryAsync(
            string localDirectory,
            string prefix = DefaultPrefix,
            IProgress<SourcifyParquetSyncProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new SourcifyParquetSyncResult();

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            var remoteFiles = await ListAllFilesAsync(prefix, cancellationToken).ConfigureAwait(false);
            var totalFiles = remoteFiles.Count;
            var processedFiles = 0;

            foreach (var remoteFile in remoteFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = remoteFile.Key.StartsWith(prefix)
                    ? remoteFile.Key.Substring(prefix.Length)
                    : remoteFile.Key;
                var localPath = Path.Combine(localDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var metaPath = localPath + ".meta";

                processedFiles++;
                progress?.Report(new SourcifyParquetSyncProgress
                {
                    CurrentFile = remoteFile.Key,
                    FilesProcessed = processedFiles,
                    TotalFiles = totalFiles
                });

                try
                {
                    if (ShouldSkipFile(localPath, metaPath, remoteFile.ETag))
                    {
                        result.FilesSkipped++;
                        continue;
                    }

                    await DownloadFileToPathAsync(remoteFile.Key, localPath, cancellationToken).ConfigureAwait(false);
                    await WriteMetaFileAsync(metaPath, remoteFile).ConfigureAwait(false);

                    result.FilesDownloaded++;
                    result.BytesDownloaded += remoteFile.Size;
                    result.DownloadedFiles.Add(localPath);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{remoteFile.Key}: {ex.Message}");
                }
            }

            return result;
        }

        public async Task<SourcifyParquetSyncResult> SyncTableToDirectoryAsync(
            string tableName,
            string localDirectory,
            IProgress<SourcifyParquetSyncProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await SyncToDirectoryAsync(localDirectory, $"{DefaultPrefix}{tableName}/", progress, cancellationToken).ConfigureAwait(false);
        }

        private string BuildListUrl(string prefix, string marker, int? maxKeys)
        {
            var url = $"{BaseUrl}?prefix={Uri.EscapeDataString(prefix)}";
            if (!string.IsNullOrEmpty(marker))
            {
                url += $"&marker={Uri.EscapeDataString(marker)}";
            }
            if (maxKeys.HasValue)
            {
                url += $"&max-keys={maxKeys.Value}";
            }
            return url;
        }

        private SourcifyParquetListResponse ParseListResponse(string xmlContent)
        {
            var doc = XDocument.Parse(xmlContent);
            var ns = doc.Root.GetDefaultNamespace();

            var response = new SourcifyParquetListResponse
            {
                Name = doc.Root.Element(ns + "Name")?.Value,
                Prefix = doc.Root.Element(ns + "Prefix")?.Value,
                Marker = doc.Root.Element(ns + "Marker")?.Value,
                NextMarker = doc.Root.Element(ns + "NextMarker")?.Value,
                IsTruncated = bool.TryParse(doc.Root.Element(ns + "IsTruncated")?.Value, out var truncated) && truncated,
                Contents = doc.Descendants(ns + "Contents")
                    .Select(c => new SourcifyParquetFileInfo
                    {
                        Key = c.Element(ns + "Key")?.Value,
                        LastModified = DateTime.Parse(c.Element(ns + "LastModified")?.Value ?? DateTime.MinValue.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                        ETag = c.Element(ns + "ETag")?.Value?.Trim('"'),
                        Size = long.TryParse(c.Element(ns + "Size")?.Value, out var size) ? size : 0
                    })
                    .ToList()
            };

            return response;
        }

        private bool ShouldSkipFile(string localPath, string metaPath, string remoteETag)
        {
            if (!File.Exists(localPath) || !File.Exists(metaPath))
            {
                return false;
            }

            try
            {
                var localETag = File.ReadAllText(metaPath).Trim();
                return string.Equals(localETag, remoteETag, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task WriteMetaFileAsync(string metaPath, SourcifyParquetFileInfo fileInfo)
        {
            var directory = Path.GetDirectoryName(metaPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(metaPath, false))
            {
                await writer.WriteAsync(fileInfo.ETag).ConfigureAwait(false);
            }
        }
    }
}
