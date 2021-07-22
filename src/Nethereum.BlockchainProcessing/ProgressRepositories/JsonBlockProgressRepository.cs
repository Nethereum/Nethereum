using Newtonsoft.Json;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.ProgressRepositories
{
    public class JsonBlockProgressRepository : IBlockProgressRepository
    {
        public class BlockProcessingProgress
        {
            public BigInteger? To { get; set; }
            public DateTimeOffset? LastUpdatedUTC { get; set; }
        }

        private BlockProcessingProgress _progress;
        private readonly Func<Task<bool>> _jsonSourceExists;
        private readonly Func<string, Task> _jsonWriter;
        private readonly Func<Task<string>> _jsonReader;
        private readonly BigInteger? _initialBlockNumber;

        public JsonBlockProgressRepository(
            Func<Task<bool>> jsonSourceExists,
            Func<string, Task> jsonWriter,
            Func<Task<string>> jsonRetriever,
            BigInteger? lastBlockProcessed = null)
        {
            this._jsonSourceExists = jsonSourceExists;
            _jsonWriter = jsonWriter;
            _jsonReader = jsonRetriever;
            _initialBlockNumber = lastBlockProcessed;
        }

        public async Task<BigInteger?> GetLastBlockNumberProcessedAsync()
        {
            await InitialiseAsync().ConfigureAwait(false);
            return _progress.To;
        }

        public async Task UpsertProgressAsync(BigInteger blockNumber)
        {
            await InitialiseAsync().ConfigureAwait(false);
            _progress.LastUpdatedUTC = DateTimeOffset.UtcNow;
            _progress.To = blockNumber;
            await PersistAsync().ConfigureAwait(false);
        }

        private async Task InitialiseAsync()
        {
            if (_progress != null) return;

            _progress = await LoadAsync().ConfigureAwait(false);

            if (_progress == null)
            {
                _progress = new BlockProcessingProgress { To = _initialBlockNumber };
                await PersistAsync().ConfigureAwait(false);
            }
            else
            {
                if (_initialBlockNumber != null) // we've been given a starting point
                {
                    if (_progress.To == null || _progress.To < _initialBlockNumber)
                    {
                        await UpsertProgressAsync(_initialBlockNumber.Value).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task<BlockProcessingProgress> LoadAsync()
        {
            if (await _jsonSourceExists.Invoke().ConfigureAwait(false) == false) return null;

            var content = await _jsonReader.Invoke().ConfigureAwait(false);
            if (content == null) return null;
            return JsonConvert.DeserializeObject<BlockProcessingProgress>(content);
        }

        private async Task PersistAsync()
        {
            await _jsonWriter.Invoke(JsonConvert.SerializeObject(_progress));
        }
    }
}
