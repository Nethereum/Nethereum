using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.TokenServices.Caching
{
    public abstract class FileStorageBase : IDisposable
    {
        private readonly string _baseDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
        private readonly Action<string, Exception> _onError;
        private bool _disposed;

        protected FileStorageBase(
            string baseDirectory,
            JsonSerializerOptions jsonOptions = null,
            Action<string, Exception> onError = null)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentNullException(nameof(baseDirectory));

            _baseDirectory = baseDirectory;
            _onError = onError;

            _jsonOptions = jsonOptions ?? CreateDefaultJsonOptions();

            Directory.CreateDirectory(_baseDirectory);
        }

        protected string BaseDirectory => _baseDirectory;

        protected static JsonSerializerOptions CreateDefaultJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        protected async Task<T> ReadAsync<T>(string relativePath, CancellationToken cancellationToken = default)
        {
            var fullPath = GetFullPath(relativePath);
            if (!File.Exists(fullPath))
                return default;

            var fileLock = GetFileLock(fullPath);
            await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
#if NET6_0_OR_GREATER
                await using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
#else
                using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
#endif
                return await JsonSerializer.DeserializeAsync<T>(fs, _jsonOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to read {relativePath}", ex);
                return default;
            }
            finally
            {
                fileLock.Release();
            }
        }

        protected async Task<bool> WriteAsync<T>(string relativePath, T value, CancellationToken cancellationToken = default)
        {
            var fullPath = GetFullPath(relativePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var fileLock = GetFileLock(fullPath);
            await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
#if NET6_0_OR_GREATER
                await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
#else
                using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
#endif
                await JsonSerializer.SerializeAsync(fs, value, _jsonOptions, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to write {relativePath}", ex);
                return false;
            }
            finally
            {
                fileLock.Release();
            }
        }

        protected async Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var fullPath = GetFullPath(relativePath);
            if (!File.Exists(fullPath))
                return true;

            var fileLock = GetFileLock(fullPath);
            await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                return true;
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to delete {relativePath}", ex);
                return false;
            }
            finally
            {
                fileLock.Release();
            }
        }

        protected bool Exists(string relativePath)
        {
            return File.Exists(GetFullPath(relativePath));
        }

        protected string GetFullPath(string relativePath)
        {
            var safePath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(_baseDirectory, safePath);
        }

        private SemaphoreSlim GetFileLock(string fullPath)
        {
            return _fileLocks.GetOrAdd(fullPath.ToLowerInvariant(), _ => new SemaphoreSlim(1, 1));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                foreach (var semaphore in _fileLocks.Values)
                {
                    semaphore.Dispose();
                }
                _fileLocks.Clear();
            }

            _disposed = true;
        }
    }
}
