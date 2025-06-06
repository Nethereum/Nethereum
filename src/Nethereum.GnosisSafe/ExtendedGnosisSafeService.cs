using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.EIP712;
using Nethereum.Contracts;
using Nethereum.Contracts.TransactionHandlers.MultiSend;
using Nethereum.GnosisSafe.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.RLP;
using Nethereum.Web3;


namespace Nethereum.GnosisSafe
{

    public partial class GnosisSafeService : ContractWeb3ServiceBase
    {
        public class SafeSignature
        {
            public string Address { get; set; }
            public string Signature { get; set; }
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
            return BuildMultiSignatureTransactionAsync(transactionData, multiSendFunction, chainId, estimateSafeTxGas, privateKeySigner);
        }

        public Task<ExecTransactionFunction> BuildMultiSendTransactionAsync(
            EncodeTransactionDataFunction transactionData,
            BigInteger chainId,
            string[] privateKeySigners,
            bool estimateSafeTxGas = false, params IMultiSendInput[] multiSendInputs)
        {
            transactionData.Operation = (int)ContractOperationType.DelegateCall;
            var multiSendFunction = new MultiSendFunction(multiSendInputs);
            return BuildMultiSignatureTransactionAsync(transactionData, multiSendFunction, chainId, estimateSafeTxGas, privateKeySigners);
        }

        public async Task<ExecTransactionFunction> BuildMultiSignatureTransactionAsync<TFunctionMessage>(
            EncodeTransactionDataFunction transactionData,
            TFunctionMessage functionMessage,
            BigInteger chainId,
            bool estimateSafeTxGas = false, params string[] privateKeySigners) where TFunctionMessage : FunctionMessage, new()
        {
            var nonce = await NonceQueryAsync().ConfigureAwait(false);
            if (estimateSafeTxGas)
            {
                var toContract = transactionData.To;
                var estimateHandler = Web3.Eth.GetContractTransactionHandler<TFunctionMessage>();
                functionMessage.FromAddress = this.ContractHandler.ContractAddress;
                var gasEstimateSafe = await estimateHandler.EstimateGasAsync(toContract, functionMessage).ConfigureAwait(false);
                transactionData.SafeTxGas = gasEstimateSafe;
            }

            transactionData.Data = functionMessage.GetCallData();
            transactionData.SafeNonce = nonce;
            return BuildTransaction(transactionData, chainId, privateKeySigners);
        }

        public byte[] GetEncodedTransactionDataHash<TFunctionMessage>(
                TFunctionMessage functionMessage,
                EncodeTransactionDataFunction transactionData,
                BigInteger chainId) where TFunctionMessage : FunctionMessage, new()
        {
            transactionData.Data = functionMessage.GetCallData();
            return GetEncodedTransactionDataHash(transactionData, chainId, this.ContractHandler.ContractAddress);
        }

        public List<SafeSignature> SignMultipleEncodedTransactionData<TFunctionMessage>(
            TFunctionMessage functionMessage,
            EncodeTransactionDataFunction transactionData,
            BigInteger chainId,
            params string[] privateKeySigners) where TFunctionMessage : FunctionMessage, new()
        {
            var hash = GetEncodedTransactionDataHash(functionMessage, transactionData, chainId);
            return SignMultipleEncodedTransactionDataHash(hash, privateKeySigners);
        }

        public async Task<string> SignEncodedTransactionDataAsync<TFunctionMessage>(TFunctionMessage functionMessage,
            EncodeTransactionDataFunction transactionData,
            BigInteger chainId, bool convertToSafeVFormat = false) where TFunctionMessage : FunctionMessage, new()
        {
            var typedData = GetGnosisSafeTypedDefinition(chainId, this.ContractHandler.ContractAddress);
            transactionData.Data = functionMessage.GetCallData();
            typedData.SetMessage(transactionData);
            var signature = await Web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(typedData.ToJson());
            if (convertToSafeVFormat)
            {
                signature = ConvertSignatureStringToGnosisVFormat(signature);
            }
            return signature;
        }

        public async Task<string> SignEncodedTransactionDataAsync(
            EncodeTransactionDataFunction transactionData,
            BigInteger chainId, bool convertToSafeVFormat = false) 
        {
            var typedData = GetGnosisSafeTypedDefinition(chainId, this.ContractHandler.ContractAddress);
            typedData.SetMessage(transactionData);
            var signature = await Web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(typedData.ToJson());
            if (convertToSafeVFormat)
            {
                signature = ConvertSignatureStringToGnosisVFormat(signature);
            }
            return signature;
        }

        public static SafeHashes GetSafeHashes
            (EncodeTransactionDataFunction transactionData, BigInteger chainId, string verifyingContractAddress)
        {
            var typedDefinition = GetGnosisSafeTypedDefinition(chainId, verifyingContractAddress);
            var safeDomainHash = Eip712TypedDataEncoder.Current.HashDomainSeparator(typedDefinition);
            var safeMessageHash = Eip712TypedDataEncoder.Current.HashStruct(transactionData, typedDefinition.PrimaryType, typeof(EncodeTransactionDataFunction));
            var safeTxnHash = Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(transactionData, typedDefinition);
            return new SafeHashes
            {
                SafeDomainHash = safeDomainHash,
                SafeMessageHash = safeMessageHash,
                SafeTxnHash = safeTxnHash
            };
        }

        public static TypedData<GnosisSafeEIP712Domain> GetGnosisSafeTypedDefinition(BigInteger chainId, string verifyingContractAddress)
        {
            return new TypedData<GnosisSafeEIP712Domain>
            {
                Domain = new GnosisSafeEIP712Domain
                {
                    ChainId = chainId,
                    VerifyingContract = verifyingContractAddress
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(GnosisSafeEIP712Domain), typeof(EncodeTransactionDataFunction)),
                PrimaryType = "SafeTx",
            };
        }

        public static EncodeTransactionDataFunction DeserialiseTransactionData(string json)
        {
#if NET6_0_OR_GREATER
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return System.Text.Json.JsonSerializer.Deserialize<EncodeTransactionDataFunction>(json, options);
#else
    return Newtonsoft.Json.JsonConvert.DeserializeObject<EncodeTransactionDataFunction>(json);
#endif
        }

        public static byte[] GetEncodedTransactionDataHash
            (string json, BigInteger chainId, string verifyingContractAddress)
        {
           var encodedTransactionData = DeserialiseTransactionData(json);
           return GetEncodedTransactionDataHash(encodedTransactionData, chainId, verifyingContractAddress);
        }

        public static byte[] GetEncodedTransactionDataHash
            (EncodeTransactionDataFunction transactionData, BigInteger chainId, string verifyingContractAddress)
        {
            var typedDefinition = GetGnosisSafeTypedDefinition(chainId, verifyingContractAddress);
            return Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(transactionData, typedDefinition);
        }

        public byte[] GetEncodedTransactionDataHash(EncodeTransactionDataFunction transactionData, BigInteger chainId)
        {
            return GetEncodedTransactionDataHash(transactionData, chainId, this.ContractHandler.ContractAddress);
        }

        public static byte[] GetHashEncoded(string json)
        {
            return Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(json);
        }

        public List<SafeSignature> SignMultipleEncodedTransactionDataHash(byte[] hashEncoded, params string[] privateKeySigners)
        {
            var messageSigner = new EthereumMessageSigner();
            var signatures = new List<SafeSignature>();

            foreach (var privateKey in privateKeySigners)
            {
                var publicAddress = EthECKey.GetPublicAddress(privateKey);
                var signatureString = messageSigner.Sign(hashEncoded, privateKey);
                signatureString = ConvertSignatureStringToGnosisVFormat(signatureString);

                signatures.Add(new SafeSignature() { Address = publicAddress, Signature = signatureString });
            }

            return signatures;
        }

        public static string ConvertSignatureStringToGnosisVFormat(string signatureString)
        {
            var signature = MessageSigner.ExtractEcdsaSignature(signatureString);
            var v = signature.V.ToBigIntegerFromRLPDecoded();
            if (VRecoveryAndChainCalculations.IsEthereumV((int)v))
            {
                signature.V = new[] { (byte)(v + 4) };
                signatureString = signature.CreateStringSignature();
            }
            return signatureString;
        }

        public ExecTransactionFunction BuildTransaction(
            EncodeTransactionDataFunction transactionData,
            BigInteger chainId,
            params string[] privateKeySigners)
        {
            var hashEncoded = GetEncodedTransactionDataHash(transactionData, chainId, this.ContractHandler.ContractAddress);
            var signatures = SignMultipleEncodedTransactionDataHash(hashEncoded, privateKeySigners);
            return BuildTransactionWithSignatures(transactionData, signatures);
        }

        public ExecTransactionFunction BuildTransactionWithSignatures(
            EncodeTransactionDataFunction transactionData,
            IEnumerable<SafeSignature> signatures)
        {
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
            var signaturesFormatted = signatures.Select(x =>  ConvertSignatureStringToGnosisVFormat(x.Signature)).ToList();
            var orderedSignatures = signaturesFormatted.OrderBy(x => x.ToLower());
            var fullSignatures = "0x";
            foreach (var signature in orderedSignatures)
            {
                fullSignatures += signature.RemoveHexPrefix();
            }
            return fullSignatures.HexToByteArray();
        }
    }
}
