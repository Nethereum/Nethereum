using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class EthGetProofRetentionGateTests : IDisposable
    {
        private const string Addr = "0x0000000000000000000000000000000000000001";
        private readonly string _dir;
        private readonly RocksDbManager _mgr;

        public EthGetProofRetentionGateTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-getproof-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
        }

        private HistoricalStateStore NewState() =>
            new HistoricalStateStore(new RocksDbStateStore(_mgr), new RocksDbStateDiffStore(_mgr), HistoricalStateOptions.Default);

        [Fact]
        public async Task GetProof_At_Unretained_Root_Throws_Not_SilentProof()
        {
            var state = NewState();
            await state.SaveAccountAsync(Addr, new Account { Nonce = 1, Balance = 5 });
            var nodeStore = new RocksDbTrieNodeStore(_mgr);

            var phantomRoot = new byte[32];
            phantomRoot[0] = 0xAB;

            var svc = new ProofService(state, nodeStore);

            await Assert.ThrowsAsync<StateNotAvailableException>(
                () => svc.GenerateAccountProofAsync(Addr, new List<BigInteger>(), phantomRoot));
        }

        [Fact]
        public async Task GetProof_With_Null_Store_Uses_Rebuild_Not_Throw()
        {
            var state = NewState();
            await state.SaveAccountAsync(Addr, new Account { Nonce = 1, Balance = 5 });

            var svc = new ProofService(state, trieNodeStore: null);

            var proof = await svc.GenerateAccountProofAsync(Addr, new List<BigInteger>(), stateRoot: null);
            Assert.NotNull(proof);
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
