using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;


namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBytecodeHealsInsideTheHealTests
    {
        private static readonly byte[] ExtraCode = new byte[] { 0x60, 0x05, 0x60, 0x06, 0x01, 0x00 };
        private static readonly byte[] ExtraCodeHash = Sha3Keccack.Current.CalculateHash(ExtraCode);

        private static CancellationTokenSource Within(int seconds) => new(TimeSpan.FromSeconds(seconds));

        private static byte[] Fill(byte b) => Enumerable.Repeat(b, 32).ToArray();

        private static BlockHeader Header(int number, byte[] root)
            => new() { BlockNumber = number, StateRoot = root, ParentHash = Fill(0x07) };

        private static Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> Answers(BlockHeader header, byte[] hash)
            => (_, _) => Task.FromResult<(BlockHeader, byte[])?>((header, hash));
        [Fact]
        public async Task Given_AMatchedPhase2WithBytecodeNeedingHeal_When_TheBootstrapRuns_Then_TheHealFetchesTheCodeBeforeCertifyAndNoBackstopRuns()
        {
            var state = new ServedState();
            state.Bytecodes.Put(ExtraCodeHash, ExtraCode);
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(ScriptedSnapHandler.Serving(state));
            await net.StartPoolAsync(1);
            await net.JoinAsync(enode);
            var header = Header(100, state.Root);
            var hash = Fill(0x01);
            using var bundle = new ScriptedReconcileBundle(Array.Empty<(byte[], byte[])>(), unresolvableCalls: 0);
            await bundle.Blocks.SaveAsync(header, hash);
            bundle.Metadata.SaveDeferredHealCodeBlob(NeedsHealCodeCodec.Encode(new[] { ExtraCodeHash }));
            var codeWasPresentAtCertify = false;
            bundle.OnReconcile = _ => codeWasPresentAtCertify = bundle.State.GetCodeAsync(ExtraCodeHash).GetAwaiter().GetResult() != null;
            var log = new RecordingLogger<SnapBytecodeHealsInsideTheHealTests>();
            using var cts = Within(120);

            await SnapBootstrapper.RunAsync(
                bundle, new SchedulerSnapPeer(net.Scheduler), header, hash, log,
                new SnapRunOptions
                {
                    Scheduler = net.Scheduler,
                    Pool = net.Pool,
                    RunBackfill = false,
                    PivotRefresher = Answers(header, hash),
                },
                cts.Token);

            Assert.True(codeWasPresentAtCertify, "the code was not fetched by the heal before certify started");
            Assert.Equal(0, log.Count("bytecode_backstop"));
            Assert.Equal(ExtraCode, await bundle.State.GetCodeAsync(ExtraCodeHash));
        }

    }
}
