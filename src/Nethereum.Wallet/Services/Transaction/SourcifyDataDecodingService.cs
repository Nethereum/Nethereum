using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Wallet.Services.Transaction
{
    public class SourcifyDataDecodingService : ITransactionDataDecodingService
    {
        private readonly IABIInfoStorage _abiStorage;
        private readonly ITransactionDataDecodingService _fallbackService;
        private readonly long _defaultChainId;

        public SourcifyDataDecodingService(
            IABIInfoStorage abiStorage,
            ITransactionDataDecodingService fallbackService = null,
            long defaultChainId = 1)
        {
            _abiStorage = abiStorage ?? throw new ArgumentNullException(nameof(abiStorage));
            _fallbackService = fallbackService;
            _defaultChainId = defaultChainId;
        }

        public async Task<TransactionDataInfo> DecodeTransactionDataAsync(string transactionData, string? contractAddress = null, long chainId = 1)
        {
            var effectiveChainId = chainId > 0 ? chainId : _defaultChainId;

            var info = new TransactionDataInfo
            {
                RawData = transactionData,
                IsContract = HasFunctionSignature(transactionData),
                ContractAddress = contractAddress
            };

            if (!info.IsContract)
                return info;

            try
            {
                if (!string.IsNullOrEmpty(contractAddress))
                {
                    var abiInfo = await _abiStorage.GetABIInfoAsync(effectiveChainId, contractAddress).ConfigureAwait(false);

                    if (abiInfo?.ContractABI != null)
                    {
                        var functionAbi = abiInfo.ContractABI.FindFunctionABIFromInputData(transactionData);

                        if (functionAbi != null)
                        {
                            info.MethodSignature = transactionData.Substring(0, 10);
                            info.FunctionName = functionAbi.Name;
                            info.TextSignature = functionAbi.Sha3Signature;
                            info.IsDecoded = true;

                            try
                            {
                                var builder = new FunctionBuilder(contractAddress, functionAbi);
                                var decodedOutput = builder.DecodeInput(transactionData).ConvertToJObject();
                                info.DecodedParameters = decodedOutput.ToString();
                            }
                            catch
                            {
                                info.DecodedParameters = null;
                            }

                            return info;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                info.DecodingError = ex.Message;
            }

            if (_fallbackService != null)
            {
                return await _fallbackService.DecodeTransactionDataAsync(transactionData, contractAddress, effectiveChainId).ConfigureAwait(false);
            }

            return info;
        }

        public bool HasFunctionSignature(string transactionData)
        {
            if (string.IsNullOrEmpty(transactionData) || !transactionData.IsHex())
                return false;

            var dataWithoutPrefix = transactionData.RemoveHexPrefix();
            return dataWithoutPrefix.Length >= 8;
        }
    }
}
