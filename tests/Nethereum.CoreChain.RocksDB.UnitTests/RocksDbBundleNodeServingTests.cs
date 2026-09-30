using System;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbBundleNodeServingTests : IDisposable
    {
        private readonly string _root;

        public RocksDbBundleNodeServingTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "necc-nodeserving-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        private RocksDbChainStoreBundle Open(string name, RocksDbStorageOptions opts)
            => RocksDbChainStoreBundle.Open(Path.Combine(_root, name), storageOptions: opts);

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "path-keyed-state", "Hash mode exposes no historical node serving")]
        public void HashMode_Default_Has_No_NodeServing()
        {
            using var bundle = Open("hash", new RocksDbStorageOptions());
            Assert.Null(bundle.NodeServing);
            Assert.Null(((IHistoricalProofServingBundle)bundle).NodeServing);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "path-keyed-state", "Path-keyed without history has no node serving")]
        public void PathKeyed_No_History_Has_No_NodeServing()
        {
            using var bundle = Open("path-nohist", new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = -1,
            });
            Assert.Null(bundle.NodeServing);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "path-keyed-state", "History without the index still has no node serving")]
        public void PathKeyed_History_On_Index_Off_Has_No_NodeServing()
        {
            using var bundle = Open("path-hist-noidx", new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 128,
                TrieNodeHistoryIndex = false,
            });
            Assert.Null(bundle.NodeServing);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "state-serving", "Enable historical state serving with path-keyed state, node history and index")]
        [NethereumDocExample(DocSection.ChainInfrastructure, "path-keyed-state", "Path-keyed with history and index exposes node serving")]
        public void PathKeyed_History_And_Index_On_Exposes_NodeServing()
        {
            using var bundle = Open("path-hist-idx", new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 128,
                TrieNodeHistoryIndex = true,
            });

            Assert.NotNull(bundle.NodeServing);
            Assert.True(bundle.NodeServing.IndexOn);
            Assert.IsType<FixedWindowFloorPolicy>(bundle.NodeServing.Floor);

            var viaSeam = ((IHistoricalProofServingBundle)bundle).NodeServing;
            Assert.Same(bundle.NodeServing, viaSeam);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "path-keyed-state", "Full archive with index exposes node serving")]
        public void PathKeyed_Full_History_And_Index_On_Exposes_NodeServing()
        {
            using var bundle = Open("path-full-idx", new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 0,
                TrieNodeHistoryIndex = true,
            });
            Assert.NotNull(bundle.NodeServing);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "state-serving", "A serving-capable node with no blocks yet resolves serve head 0 instead of crashing")]
        public async System.Threading.Tasks.Task FreshNode_NoBlocks_ResolveForRoot_DoesNotOverflow()
        {
            using var bundle = Open("path-fresh-empty", new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 128,
                TrieNodeHistoryIndex = true,
            });
            Assert.NotNull(bundle.NodeServing);

            var root = new byte[32];
            for (int i = 0; i < 32; i++) root[i] = (byte)(0x5A ^ i);

            var result = await bundle.NodeServing.ResolveForRootAsync(root);
            Assert.Null(result);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
