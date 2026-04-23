using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthSendRawTransactionHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_sendRawTransaction.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var rawTxHex = GetParam<string>(request, 0);

            ISignedTransaction signedTx;
            BlobSidecar sidecar = null;
            try
            {
                var rawTxBytes = rawTxHex.HexToByteArray();
                signedTx = TransactionFactory.CreateTransaction(rawTxBytes);

                if (signedTx is Transaction4844 blobTx && blobTx.Sidecar != null)
                {
                    sidecar = blobTx.Sidecar;
                    blobTx.Sidecar = null;
                }
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32602, $"Invalid transaction: {ex.Message}");
            }

            TransactionExecutionResult result;
            try
            {
                result = await context.Node.SendTransactionAsync(signedTx);
            }
            catch (InvalidOperationException ex)
            {
                return Error(request.Id, -32000, ex.Message);
            }

            if (result == null)
            {
                return Error(request.Id, -32603, "Internal error: SendTransactionAsync returned null");
            }

            if (result.Success || result.Receipt != null)
            {
                if (result.TransactionHash == null)
                {
                    return Error(request.Id, -32603, "Internal error: TransactionHash is null");
                }

                if (sidecar != null && context.Node.BlobStore != null && signedTx is Transaction4844 blob4844)
                {
                    var blockNum = await context.Node.GetBlockNumberAsync();
                    await StoreBlobSidecarAsync(context, blockNum, result, blob4844, sidecar);
                }

                return Success(request.Id, result.TransactionHash.ToHex(true));
            }

            return Error(request.Id, -32000, result.RevertReason ?? "Transaction rejected");
        }

        private static async Task StoreBlobSidecarAsync(
            RpcContext context, System.Numerics.BigInteger blockNumber,
            TransactionExecutionResult result, Transaction4844 tx, BlobSidecar sidecar)
        {
            var records = new List<Storage.BlobSidecarRecord>();

            for (int i = 0; i < sidecar.Blobs.Count; i++)
            {
                records.Add(new Storage.BlobSidecarRecord
                {
                    Index = i,
                    Blob = sidecar.Blobs[i],
                    KzgCommitment = i < sidecar.Commitments.Count ? sidecar.Commitments[i] : null,
                    KzgProof = i < sidecar.Proofs.Count ? sidecar.Proofs[i] : null,
                    VersionedHash = i < tx.BlobVersionedHashes.Count ? tx.BlobVersionedHashes[i] : null,
                    TransactionHash = result.TransactionHash
                });
            }

            await context.Node.BlobStore.StoreBlobsAsync(blockNumber, result.TransactionHash, records);
        }
    }
}
