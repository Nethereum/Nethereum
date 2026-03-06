using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.Sync
{
    public class StateSnapshotImporter : IStateSnapshotImporter
    {
        private readonly IStateStore _stateStore;
        private readonly IStateSnapshotReader _snapshotReader;
        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public StateSnapshotImporter(IStateStore stateStore, IStateSnapshotReader snapshotReader = null)
        {
            _stateStore = stateStore;
            _snapshotReader = snapshotReader ?? new StateSnapshotReader();
        }

        public async Task<StateSnapshotImportResult> ImportSnapshotAsync(
            Stream snapshotStream,
            byte[] expectedStateRoot = null,
            bool verifyStateRoot = true,
            CancellationToken cancellationToken = default)
        {
            var result = new StateSnapshotImportResult();

            try
            {
                var header = await _snapshotReader.ReadHeaderAsync(snapshotStream, cancellationToken);
                snapshotStream.Position = 0;

                if (verifyStateRoot && expectedStateRoot != null)
                {
                    if (!ByteUtil.AreEqual(header.StateRoot, expectedStateRoot))
                    {
                        result.Success = false;
                        result.ErrorMessage = "State root mismatch in header";
                        return result;
                    }
                }

                snapshotStream.Position = 0;
                long accountsImported = 0;
                long storageSlotsImported = 0;
                long codesImported = 0;

                await foreach (var account in _snapshotReader.ReadAccountsAsync(snapshotStream, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var modelAccount = new Account
                    {
                        Nonce = account.Nonce,
                        Balance = account.Balance,
                        CodeHash = account.CodeHash,
                        StateRoot = account.StorageRoot
                    };

                    await _stateStore.SaveAccountAsync(account.Address, modelAccount);
                    accountsImported++;
                }

                snapshotStream.Position = 0;
                var seenAddresses = new System.Collections.Generic.HashSet<string>();

                await foreach (var account in _snapshotReader.ReadAccountsAsync(snapshotStream, cancellationToken))
                {
                    if (seenAddresses.Contains(account.Address))
                        continue;
                    seenAddresses.Add(account.Address);

                    snapshotStream.Position = 0;
                    await foreach (var slot in _snapshotReader.ReadStorageSlotsAsync(snapshotStream, account.Address, cancellationToken))
                    {
                        await _stateStore.SaveStorageAsync(slot.Address, slot.Slot, slot.Value);
                        storageSlotsImported++;
                    }
                }

                snapshotStream.Position = 0;
                await foreach (var code in _snapshotReader.ReadCodesAsync(snapshotStream, cancellationToken))
                {
                    await _stateStore.SaveCodeAsync(code.CodeHash, code.Code);
                    codesImported++;
                }

                result.Success = true;
                result.SnapshotInfo = new StateSnapshotInfo
                {
                    BlockNumber = header.BlockNumber,
                    ChainId = header.ChainId,
                    StateRoot = header.StateRoot,
                    AccountCount = accountsImported,
                    StorageSlotCount = storageSlotsImported,
                    CodeCount = codesImported
                };
                result.AccountsImported = accountsImported;
                result.StorageSlotsImported = storageSlotsImported;
                result.CodesImported = codesImported;
                result.StateRootVerified = verifyStateRoot && expectedStateRoot != null;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<StateSnapshotImportResult> ImportSnapshotFromFileAsync(
            string filePath,
            byte[] expectedStateRoot = null,
            bool verifyStateRoot = true,
            bool compressed = true,
            CancellationToken cancellationToken = default)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            Stream inputStream = fileStream;

            if (compressed)
            {
                using var memoryStream = new MemoryStream();
                using (var decompress = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress))
                {
                    await decompress.CopyToAsync(memoryStream, cancellationToken);
                }
                memoryStream.Position = 0;
                return await ImportSnapshotAsync(memoryStream, expectedStateRoot, verifyStateRoot, cancellationToken);
            }

            return await ImportSnapshotAsync(inputStream, expectedStateRoot, verifyStateRoot, cancellationToken);
        }

    }
}
