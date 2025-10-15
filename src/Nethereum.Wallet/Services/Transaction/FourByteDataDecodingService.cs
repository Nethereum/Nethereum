using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.DataServices.FourByteDirectory;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Wallet.Services.Transaction
{
    public class FourByteDataDecodingService : ITransactionDataDecodingService
    {
        private readonly FourByteDirectoryService _fourByteService;

        public FourByteDataDecodingService(FourByteDirectoryService fourByteService)
        {
            _fourByteService = fourByteService;
        }

        public async Task<TransactionDataInfo> DecodeTransactionDataAsync(string transactionData, string? contractAddress = null)
        {
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
                var methodSignature = transactionData.Substring(0, 10);
                var response = await _fourByteService.GetFunctionSignatureByHexSignatureAsync(methodSignature);

                if (response?.Signatures?.Count > 0)
                {
                    var signature = response.Signatures.OrderBy(s => s.Id).First();
                    info.MethodSignature = methodSignature;
                    info.FunctionName = ExtractFunctionName(signature.TextSignature);
                    info.TextSignature = signature.TextSignature;
                    
                    try
                    {
                        var abiSignature = $"function {signature.TextSignature}";
                        var contractAbi = ABIDeserialiserFactory.DeserialiseContractABI(abiSignature);
                        var fnAbi = contractAbi.Functions.FirstOrDefault();
                        
                        if (fnAbi != null)
                        {
                            var builder = new FunctionBuilder(contractAddress ?? "0x0000000000000000000000000000000000000000", fnAbi);
                            var decodedOutput = builder.DecodeInput(transactionData).ConvertToJObject();
                            info.DecodedParameters = decodedOutput.ToString();
                        }
                    }
                    catch (Exception)
                    {
                        // If decoding fails, just show the signature without parameters
                        info.DecodedParameters = null;
                    }
                    
                    info.IsDecoded = true;
                }
            }
            catch (Exception ex)
            {
                info.DecodingError = ex.Message;
            }

            return info;
        }

        public bool HasFunctionSignature(string transactionData)
        {
            if (string.IsNullOrEmpty(transactionData) || !transactionData.IsHex()) 
                return false;
                
            var dataWithoutPrefix = transactionData.RemoveHexPrefix();
            return dataWithoutPrefix.Length >= 8; // At least 4 bytes (8 hex chars) for function selector
        }

        private string ExtractFunctionName(string textSignature)
        {
            var parenIndex = textSignature.IndexOf('(');
            return parenIndex > 0 ? textSignature.Substring(0, parenIndex) : textSignature;
        }
    }
}
