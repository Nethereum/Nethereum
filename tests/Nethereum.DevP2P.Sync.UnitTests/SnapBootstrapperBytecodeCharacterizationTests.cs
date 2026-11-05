using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperBytecodeCharacterizationTests
    {
        private static readonly byte[] CodeA = { 0x60, 0x01, 0xf3 };

        private static byte[] Keccak(byte[] data) => Sha3Keccack.Current.CalculateHash(data);

        private static CharScheduler ServingCodes(params byte[][] served)
            => new() { ByteCodes = (hashes, ct) => Task.FromResult(new ByteCodesMessage { Codes = served.ToList() }) };

        [Fact]
        public async Task Given_ADeferredCodeBlobWhoseCodeIsAlreadyStored_When_TheBackstopRuns_Then_TheBlobIsClearedWithoutAnyHeal()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await bundle.State.SaveCodeAsync(Keccak(CodeA), CodeA);
            bundle.Metadata.SaveDeferredHealCodeBlob(NeedsHealCodeCodec.Encode(new[] { Keccak(CodeA) }));
            var requested = 0;
            var scheduler = new CharScheduler
            {
                ByteCodes = (hashes, ct) => { requested++; return Task.FromResult(new ByteCodesMessage { Codes = new List<byte[]>() }); },
                TrieNodes = (root, paths, ct) => { requested++; return Task.FromResult(new TrieNodesMessage { Nodes = new List<byte[]>() }); },
            };
            var log = new CharLog();

            await SnapBootstrapper.FetchMissingBytecodeAsync(
                bundle, scheduler, resumeFrom: null, CharBytes.Hash32(0x01), log, CancellationToken.None);

            Assert.Null(bundle.Metadata.GetDeferredHealCodeBlob());
            Assert.Equal(0, requested);
            Assert.False(log.Saw("snap.phase3.bytecode_backstop"));
        }
    }
}
