using Nethereum.ABI.Model;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.ABI.ABIRepository
{
    public class ABIBatchResult
    {
        public IDictionary<string, FunctionABI> Functions { get; set; } = new Dictionary<string, FunctionABI>();
        public IDictionary<string, EventABI> Events { get; set; } = new Dictionary<string, EventABI>();
    }

    public interface IABIInfoStorage
    {
        void AddABIInfo(ABIInfo abiInfo);
        ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature);
        List<ErrorABI> FindErrorABI(string signature);
        EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature);
        List<EventABI> FindEventABI(string signature);
        FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature);
        List<FunctionABI> FindFunctionABI(string signature);
        FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData);
        List<FunctionABI> FindFunctionABIFromInputData(string inputData);
        ABIInfo GetABIInfo(BigInteger chainId, string contractAddress);

        Task<ABIInfo> GetABIInfoAsync(long chainId, string contractAddress);
        Task<FunctionABI> FindFunctionABIAsync(BigInteger chainId, string contractAddress, string signature);
        Task<FunctionABI> FindFunctionABIFromInputDataAsync(BigInteger chainId, string contractAddress, string inputData);
        Task<EventABI> FindEventABIAsync(BigInteger chainId, string contractAddress, string signature);
        Task<ErrorABI> FindErrorABIAsync(BigInteger chainId, string contractAddress, string signature);

        Task<IDictionary<string, FunctionABI>> FindFunctionABIsBatchAsync(IEnumerable<string> signatures);
        Task<IDictionary<string, EventABI>> FindEventABIsBatchAsync(IEnumerable<string> signatures);
        Task<ABIBatchResult> FindABIsBatchAsync(IEnumerable<string> functionSignatures, IEnumerable<string> eventSignatures);
    }
}