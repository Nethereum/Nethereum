using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    public partial class SnapSyncClient
    {
        private readonly record struct BytecodeWriteResult(int Count, ulong Bytes, IReadOnlyList<byte[]> Deferred);

        private async Task<BytecodeWriteResult> FetchAndWriteBytecodesAsync(
            IReadOnlyList<byte[]> codeHashes,
            ulong requestIdSeed,
            Action markProductive,
            CancellationToken ct)
        {
            var deduped = DedupeAndFilterCodeHashes(new List<byte[]>(codeHashes));
            if (deduped.Count == 0) return new BytecodeWriteResult(0, 0, Array.Empty<byte[]>());

            var keccak = Sha3Keccack.Current;
            long codeReqIdCounter = (long)requestIdSeed;
            var missing = new ConcurrentDictionary<byte[], byte>(ByteArrayComparer.Current);
            foreach (var hash in deduped) missing.TryAdd(hash, 0);

            int noProgressRounds = 0;
            int written = 0;
            ulong bytes = 0;

            while (!missing.IsEmpty && noProgressRounds < BytecodeDeadEndNoProgressRounds)
            {
                ct.ThrowIfCancellationRequested();
                int before = missing.Count;
                var toFetch = new List<byte[]>(missing.Keys);

                for (int offset = 0; offset < toFetch.Count; offset += MaxCodeRequestCount)
                {
                    ct.ThrowIfCancellationRequested();
                    var count = Math.Min(MaxCodeRequestCount, toFetch.Count - offset);
                    var chunk = toFetch.GetRange(offset, count);
                    ByteCodesMessage codesResp;
                    try
                    {
                        codesResp = await _peer.GetByteCodesAsync(new GetByteCodesMessage
                        {
                            RequestId = (ulong)Interlocked.Increment(ref codeReqIdCounter),
                            Hashes = chunk,
                            ResponseBytes = _responseBytesBudget
                        },
                        verifyResponse: r => SnapProofVerifier.VerifyByteCodesResponse(chunk, r),
                        ct).ConfigureAwait(false);
                    }
                    catch (FetchRequestFailedException)
                    {
                        continue;
                    }

                    var (chunkWritten, chunkBytes) = await WriteReturnedBytecodesAsync(
                        codesResp, keccak, missing, markProductive, ct).ConfigureAwait(false);
                    written += chunkWritten;
                    bytes += chunkBytes;
                }

                if (missing.Count < before)
                {
                    noProgressRounds = 0;
                }
                else
                {
                    noProgressRounds++;
                    if (!missing.IsEmpty)
                        await Task.Delay(SnapAccountRangeRetryDelayMs, ct).ConfigureAwait(false);
                }
            }

            if (!missing.IsEmpty)
            {
                var deferred = new List<byte[]>(missing.Keys);
                _logger.LogWarning(
                    "snap.phase2.bytecode_obligation.deferred_to_heal missing={Missing} — no peer served these codes within the patience window ({Rounds} no-progress rounds); deferring to Phase-3 heal and advancing the account cursor",
                    deferred.Count, noProgressRounds);
                return new BytecodeWriteResult(written, bytes, deferred);
            }

            return new BytecodeWriteResult(written, bytes, Array.Empty<byte[]>());
        }

        private async Task<(int Written, ulong Bytes)> WriteReturnedBytecodesAsync(
            ByteCodesMessage codesResp,
            Sha3Keccack keccak,
            ConcurrentDictionary<byte[], byte> missing,
            Action markProductive,
            CancellationToken ct)
        {
            int written = 0;
            ulong bytes = 0;
            foreach (var code in codesResp.Codes)
            {
                if (code == null || code.Length == 0) continue;
                var hash = keccak.CalculateHash(code);
                if (!missing.TryRemove(hash, out _)) continue;
                await _sink.WriteBytecodeAsync(hash, code, ct).ConfigureAwait(false);
                markProductive();
                written++;
                bytes += (ulong)code.Length;
                _logger.LogDebug(
                    "snap.phase2.bytecode_obligation.completed code_hash=0x{CodeHash} state=bytecode_written",
                    hash.ToHex());
            }
            return (written, bytes);
        }

        private static List<byte[]> DedupeAndFilterCodeHashes(List<byte[]> hashes)
        {
            var seen = new HashSet<byte[]>(ByteArrayComparer.Current);
            var result = new List<byte[]>(hashes.Count);
            foreach (var h in hashes)
            {
                if (h == null || h.Length == 0) continue;
                if (ByteUtil.AreEqual(h, DefaultValues.EMPTY_DATA_HASH)) continue;
                if (seen.Add(h)) result.Add(h);
            }
            return result;
        }
    }
}
