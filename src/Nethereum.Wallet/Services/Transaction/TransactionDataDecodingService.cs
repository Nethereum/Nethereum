using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Wallet.Services.Transaction
{
    public interface ITransactionDataDecodingService
    {
        Task<TransactionDataInfo> DecodeTransactionDataAsync(string transactionData, string? contractAddress = null);
        bool HasFunctionSignature(string transactionData);
    }

    public class TransactionDataInfo
    {
        public string RawData { get; set; } = "";
        public int DataSizeInBytes => RawData?.IsHex() == true 
            ? RawData.RemoveHexPrefix().Length / 2 
            : 0;
        public bool IsContract { get; set; }
        public bool IsDecoded { get; set; }
        
        public string? ContractAddress { get; set; }
        public string? MethodSignature { get; set; }
        public string? FunctionName { get; set; }
        public string? TextSignature { get; set; }
        public string? DecodedParameters { get; set; }
        
        public string? DecodingError { get; set; }
        public bool HasError => !string.IsNullOrEmpty(DecodingError);
        public bool HasDecodedParameters => !string.IsNullOrEmpty(DecodedParameters);
    }
}