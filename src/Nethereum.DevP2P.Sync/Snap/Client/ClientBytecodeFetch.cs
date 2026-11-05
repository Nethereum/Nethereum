using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class ClientBytecodeFetch
    {
        internal readonly record struct Result(int Count, ulong Bytes, IReadOnlyList<byte[]> Deferred);

        private readonly SnapSyncClient _client;

        public ClientBytecodeFetch(SnapSyncClient client)
        {
            _client = client;
        }

        public async Task<Result> RunAsync(
            IReadOnlyList<byte[]> codeHashes,
            ulong requestIdSeed,
            Action markProductive,
            CancellationToken ct)
        {
            var deduped = SnapByteCodes.Requestable(codeHashes);
            if (deduped.Count == 0) return new Result(0, 0, Array.Empty<byte[]>());

            long codeReqIdCounter = (long)requestIdSeed;
            var missing = new HashSet<byte[]>(deduped, ByteArrayComparer.Current);
            int written = 0;
            ulong bytes = 0;

            var noProgressRounds = await BytecodeFetchRounds.RunAsync(
                missing, _client.BytecodeDeadEndNoProgressRounds, SnapPeerRequests.RetryDelayMs,
                (chunk, token) => FetchCodesAsync(chunk, (ulong)Interlocked.Increment(ref codeReqIdCounter), token),
                async (hash, code) =>
                {
                    await WriteCodeAsync(hash, code, markProductive, ct).ConfigureAwait(false);
                    written++;
                    bytes += (ulong)code.Length;
                }, ct).ConfigureAwait(false);

            return DeferTail(missing, noProgressRounds, written, bytes);
        }

        private Result DeferTail(HashSet<byte[]> missing, int noProgressRounds, int written, ulong bytes)
        {
            if (missing.Count > 0)
            {
                var deferred = new List<byte[]>(missing);
                _client.Logger.LogWarning(
                    "snap.phase2.bytecode_obligation.deferred_to_heal missing={Missing} — no peer served these codes within the patience window ({Rounds} no-progress rounds); deferring to Phase-3 heal and advancing the account cursor",
                    deferred.Count, noProgressRounds);
                return new Result(written, bytes, deferred);
            }

            return new Result(written, bytes, Array.Empty<byte[]>());
        }

        private async Task<IReadOnlyList<byte[]>> FetchCodesAsync(List<byte[]> chunk, ulong requestId, CancellationToken ct)
        {
            try
            {
                var resp = await _client.Peer.GetByteCodesAsync(new GetByteCodesMessage
                {
                    RequestId = requestId,
                    Hashes = chunk,
                    ResponseBytes = _client.ResponseBytesBudget
                },
                verifyResponse: r => SnapProofVerifier.VerifyByteCodesResponse(chunk, r),
                ct).ConfigureAwait(false);
                return resp.Codes;
            }
            catch (FetchRequestFailedException)
            {
                return Array.Empty<byte[]>();
            }
        }

        private async Task WriteCodeAsync(byte[] hash, byte[] code, Action markProductive, CancellationToken ct)
        {
            await _client.Sink.WriteBytecodeAsync(hash, code, ct).ConfigureAwait(false);
            markProductive();
            _client.Logger.LogDebug(
                "snap.phase2.bytecode_obligation.completed code_hash=0x{CodeHash} state=bytecode_written",
                hash.ToHex());
        }
    }
}
