using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Contracts.TransactionHandlers.MultiSend;
using Nethereum.GnosisSafe.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;

namespace Nethereum.GnosisSafe
{
    public partial class GnosisSafeService
    {
        public class SafeSignature
        {
            public string Address { get; set; }
            public string Signature { get; set; }
        }

        public Task<string> VersionQueryAsync(VERSIONFunction versionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VERSIONFunction, string>(versionFunction, blockParameter);
        }

        public Task<string> VersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VERSIONFunction, string>(null, blockParameter);
        }

        public async Task<ExecTransactionFunction> BuildTransactionAsync(
            EncodeTransactionDataFunction transactionData,
            BigInteger chainId,
            bool estimateSafeTxGas = false, params string[] privateKeySigners)
        {
            var nonce = await NonceQueryAsync().ConfigureAwait(false);
            transactionData.SafeNonce = nonce;

            return BuildTransaction(transactionData, chainId, privateKeySigners);
        }


        public Task<ExecTransactionFunction> BuildMultiSendTransactionAsync(
            EncodeTransactionDataFunction transactionData,
            BigInteger chainId,
            string privateKeySigner,
            bool estimateSafeTxGas = false, params IMultiSendInput[] multiSendInputs)
        {
            transactionData.Operation = (int)ContractOperationType.DelegateCall;
            var multiSendFunction = new MultiSendFunction(multiSendInputs);
            return BuildTransactionAsync(transactionData, multiSendFunction, chainId, estimateSafeTxGas,
                privateKeySigner);
        }

        public Task<ExecTransactionFunction> BuildMultiSendTransactionAsync(
            EncodeTransactionDataFunction transactionData,
            BigInteger chainId,
            string[] privateKeySigners,
            bool estimateSafeTxGas = false, params IMultiSendInput[] multiSendInputs)
        {
            transactionData.Operation = (int)ContractOperationType.DelegateCall;
            var multiSendFunction = new MultiSendFunction(multiSendInputs);
            return BuildTransactionAsync(transactionData, multiSendFunction, chainId, estimateSafeTxGas,
                privateKeySigners);
        }


        public async Task<ExecTransactionFunction> BuildTransactionAsync<TFunctionMessage>(
            EncodeTransactionDataFunction transactionData,
            TFunctionMessage functionMessage,
            BigInteger chainId,
             bool estimateSafeTxGas = false, params string[] privateKeySigners) where TFunctionMessage : FunctionMessage, new()
        {
            var nonce = await NonceQueryAsync().ConfigureAwait(false); ;

            if (estimateSafeTxGas)
            {
                var toContract = transactionData.To;
                var estimateHandler = Web3.Eth.GetContractTransactionHandler<TFunctionMessage>();
                functionMessage.FromAddress = this.ContractHandler.ContractAddress;
                var gasEstimateSafe = await estimateHandler.EstimateGasAsync(toContract, functionMessage).ConfigureAwait(false); ;
                transactionData.SafeTxGas = gasEstimateSafe;
            }

            transactionData.Data = functionMessage.GetCallData();
            transactionData.SafeNonce = nonce;

            return BuildTransaction(transactionData, chainId, privateKeySigners);
        }

        public ExecTransactionFunction BuildTransaction(
            EncodeTransactionDataFunction transactionData,
            BigInteger chainId,
            params string[] privateKeySigners)
        {
            var signer = new Eip712TypedDataSigner();
            var messageSigner = new MessageSigner();

            var domain = new Domain
            {
                VerifyingContract = this.ContractHandler.ContractAddress,
                ChainId = chainId
            };

            var encodedMessage = signer.EncodeTypedData(transactionData, domain, "SafeTx");
            var hashEncoded = Util.Sha3Keccack.Current.CalculateHash(encodedMessage);
            var signatures = new List<SafeSignature>();
            foreach (var privateKey in privateKeySigners)
            {
                var publicAddress = EthECKey.GetPublicAddress(privateKey);
                var signature = messageSigner.Sign(hashEncoded, privateKey);
                signatures.Add(new SafeSignature() { Address = publicAddress, Signature = signature });
            }

            var fullSignature = GetCombinedSignaturesInOrder(signatures);

            return new ExecTransactionFunction()
            {
                To = transactionData.To,
                Value = transactionData.Value,
                Data = transactionData.Data,
                Operation = transactionData.Operation,
                SafeTxGas = transactionData.SafeTxGas,
                BaseGas = transactionData.BaseGas,
                SafeGasPrice = transactionData.SafeGasPrice,
                GasToken = transactionData.GasToken,
                RefundReceiver = transactionData.RefundReceiver,
                Signatures = fullSignature

            };
        }

        public byte[] GetCombinedSignaturesInOrder(IEnumerable<SafeSignature> signatures)
        {
            var orderedSignatures = signatures.OrderBy(x => x.Signature.ToLower());
            var fullSignatures = "0x";
            foreach (var signature in orderedSignatures)
            {
                fullSignatures += signature.Signature.RemoveHexPrefix();
            }

            return fullSignatures.HexToByteArray();
        }
    }
}
