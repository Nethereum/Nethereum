using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring;

namespace Nethereum.AppChain.Sync
{
    public class BatchSyncService : IBatchSyncService, IDisposable
    {
        private readonly BatchSyncConfig _config;
        private readonly IBatchStore _batchStore;
        private readonly IBatchImporter _batchImporter;
        private readonly IAnchorService _anchorService;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);

        private BatchSyncState _state = BatchSyncState.Idle;
        private BigInteger _localTip = BigInteger.MinusOne;
        private BigInteger _anchoredTip = BigInteger.MinusOne;
        private CancellationTokenSource _syncCts;
        private bool _disposed;

        public BatchSyncState State => _state;
        public BigInteger LocalTip => _localTip;
        public BigInteger AnchoredTip => _anchoredTip;

        public event EventHandler<BatchSyncProgressEventArgs> Progress;
        public event EventHandler<BatchImportedEventArgs> BatchImported;
        public event EventHandler<SyncErrorEventArgs> Error;

        public BatchSyncService(
            BatchSyncConfig config,
            IBatchStore batchStore,
            IBatchImporter batchImporter,
            IAnchorService anchorService,
            HttpClient httpClient = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _batchStore = batchStore ?? throw new ArgumentNullException(nameof(batchStore));
            _batchImporter = batchImporter ?? throw new ArgumentNullException(nameof(batchImporter));
            _anchorService = anchorService ?? throw new ArgumentNullException(nameof(anchorService));
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_state != BatchSyncState.Idle)
                return;

            _syncCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _localTip = await _batchStore.GetLatestImportedBlockAsync();
            await RefreshAnchoredTipAsync(_syncCts.Token);
        }

        public async Task StopAsync()
        {
            _syncCts?.Cancel();
            await _syncLock.WaitAsync();
            try
            {
                _state = BatchSyncState.Idle;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<BatchSyncResult> SyncToLatestAsync(CancellationToken cancellationToken = default)
        {
            await RefreshAnchoredTipAsync(cancellationToken);
            return await SyncToBlockAsync(_anchoredTip, cancellationToken);
        }

        public async Task<BatchSyncResult> SyncToBlockAsync(BigInteger targetBlock, CancellationToken cancellationToken = default)
        {
            if (!await _syncLock.WaitAsync(0, cancellationToken))
            {
                return new BatchSyncResult
                {
                    Success = false,
                    ErrorMessage = "Sync already in progress"
                };
            }

            var stopwatch = Stopwatch.StartNew();
            var result = new BatchSyncResult
            {
                StartBlock = _localTip + 1
            };

            try
            {
                _localTip = await _batchStore.GetLatestImportedBlockAsync();

                if (_localTip >= targetBlock)
                {
                    result.Success = true;
                    result.EndBlock = _localTip;
                    _state = BatchSyncState.Synced;
                    return result;
                }

                var batchesSynced = 0;
                var blocksSynced = 0;
                var currentBlock = _localTip + 1;

                while (currentBlock <= targetBlock)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batchEnd = currentBlock + _config.BatchSize - 1;
                    if (batchEnd > targetBlock)
                        batchEnd = targetBlock;

                    _state = BatchSyncState.FetchingAnchors;
                    var anchor = await _anchorService.GetAnchorAsync(batchEnd);

                    if (anchor == null)
                    {
                        if (batchEnd < _anchoredTip)
                        {
                            OnError($"No anchor found for block {batchEnd}", null, false);
                            result.ErrorMessage = $"No anchor found for block {batchEnd}";
                            _state = BatchSyncState.Error;
                            return result;
                        }
                        break;
                    }

                    OnProgress(BatchSyncState.DownloadingBatch, currentBlock, targetBlock, $"{currentBlock}-{batchEnd}");
                    _state = BatchSyncState.DownloadingBatch;

                    var downloadResult = await DownloadBatchAsync(currentBlock, batchEnd, cancellationToken);
                    if (!downloadResult.Success)
                    {
                        OnError($"Failed to download batch {currentBlock}-{batchEnd}: {downloadResult.ErrorMessage}", null, true);
                        result.ErrorMessage = downloadResult.ErrorMessage;
                        _state = BatchSyncState.Error;
                        return result;
                    }

                    OnProgress(BatchSyncState.ImportingBatch, currentBlock, targetBlock, $"{currentBlock}-{batchEnd}");
                    _state = BatchSyncState.ImportingBatch;

                    var importStartTime = Stopwatch.StartNew();
                    var importResult = await _batchImporter.ImportBatchFromFileAsync(
                        downloadResult.LocalPath,
                        downloadResult.BatchInfo?.BatchHash,
                        _config.DefaultVerificationMode,
                        _config.CompressBatches,
                        cancellationToken);

                    if (!importResult.Success)
                    {
                        OnError($"Failed to import batch {currentBlock}-{batchEnd}: {importResult.ErrorMessage}", null, true);
                        result.ErrorMessage = importResult.ErrorMessage;
                        _state = BatchSyncState.Error;
                        return result;
                    }

                    importStartTime.Stop();
                    batchesSynced++;
                    blocksSynced += importResult.BlocksImported;
                    _localTip = batchEnd;

                    OnBatchImported(importResult.BatchInfo, importResult.BlocksImported, importStartTime.Elapsed);

                    if (File.Exists(downloadResult.LocalPath))
                    {
                        File.Delete(downloadResult.LocalPath);
                    }

                    currentBlock = batchEnd + 1;
                }

                stopwatch.Stop();
                result.Success = true;
                result.EndBlock = _localTip;
                result.BatchesSynced = batchesSynced;
                result.BlocksSynced = blocksSynced;
                result.Duration = stopwatch.Elapsed;
                _state = BatchSyncState.Synced;

                return result;
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "Sync cancelled";
                _state = BatchSyncState.Idle;
                return result;
            }
            catch (Exception ex)
            {
                OnError($"Sync failed: {ex.Message}", ex, false);
                result.ErrorMessage = ex.Message;
                _state = BatchSyncState.Error;
                return result;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                _syncLock.Release();
            }
        }

        public async Task<BatchDownloadResult> DownloadBatchAsync(BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken = default)
        {
            var result = new BatchDownloadResult();
            var fileName = BatchFileFormat.GetBatchFileName((long)_config.ChainId, (long)fromBlock, (long)toBlock, _config.CompressBatches);

            var urls = GetBatchUrls(fileName);

            foreach (var url in urls)
            {
                try
                {
                    var localPath = Path.Combine(_config.BatchOutputDirectory ?? Path.GetTempPath(), fileName);

                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await contentStream.CopyToAsync(fileStream, cancellationToken);

                    result.Success = true;
                    result.LocalPath = localPath;
                    result.SourceUrl = url;
                    result.BytesDownloaded = new FileInfo(localPath).Length;
                    return result;
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = ex.Message;
                }
            }

            result.Success = false;
            if (string.IsNullOrEmpty(result.ErrorMessage))
                result.ErrorMessage = "No available sources";

            return result;
        }

        private string[] GetBatchUrls(string fileName)
        {
            var count = 1 + (_config.MirrorUrls?.Length ?? 0);
            var urls = new string[count];
            var index = 0;

            if (!string.IsNullOrEmpty(_config.SequencerUrl))
            {
                urls[index++] = $"{_config.SequencerUrl.TrimEnd('/')}/batches/{fileName}";
            }

            if (_config.MirrorUrls != null)
            {
                foreach (var mirror in _config.MirrorUrls)
                {
                    urls[index++] = $"{mirror.TrimEnd('/')}/batches/{fileName}";
                }
            }

            return urls;
        }

        private async Task RefreshAnchoredTipAsync(CancellationToken cancellationToken)
        {
            try
            {
                _anchoredTip = await _anchorService.GetLatestAnchoredBlockAsync();
            }
            catch (Exception ex)
            {
                OnError($"Failed to refresh anchored tip: {ex.Message}", ex, true);
            }
        }

        private void OnProgress(BatchSyncState state, BigInteger currentBlock, BigInteger targetBlock, string batchId)
        {
            _state = state;
            var percent = targetBlock > 0
                ? (double)(currentBlock - 1) / (double)targetBlock * 100.0
                : 0;

            Progress?.Invoke(this, new BatchSyncProgressEventArgs
            {
                State = state,
                CurrentBlock = currentBlock,
                TargetBlock = targetBlock,
                ProgressPercent = percent,
                CurrentBatchId = batchId
            });
        }

        private void OnBatchImported(BatchInfo batchInfo, int blocksImported, TimeSpan duration)
        {
            BatchImported?.Invoke(this, new BatchImportedEventArgs
            {
                BatchInfo = batchInfo,
                BlocksImported = blocksImported,
                ImportDuration = duration
            });
        }

        private void OnError(string message, Exception ex, bool recoverable)
        {
            Error?.Invoke(this, new SyncErrorEventArgs
            {
                Message = message,
                Exception = ex,
                Recoverable = recoverable
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _syncCts?.Cancel();
            _syncCts?.Dispose();
            _syncLock?.Dispose();
        }
    }
}
