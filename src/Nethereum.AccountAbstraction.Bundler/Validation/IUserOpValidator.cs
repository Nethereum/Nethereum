using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.Bundler.Validation
{
    public interface IUserOpValidator
    {
        Task<UserOpValidationResult> ValidateAsync(PackedUserOperation userOp, string entryPoint);

        Task<UserOpValidationResult> ValidateAsync(
            PackedUserOperation userOp, string entryPoint, ISet<string> accessedStorageAddresses,
            Authorisation eip7702Auth = null);

        Task<UserOpValidationResult> ValidateStructureAsync(
            PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null);

        Task<UserOpValidationResult> SimulateValidationAsync(
            PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null);

        Task<UserOpValidationResult> EstimateGasAsync(UserOperation userOp, string entryPoint);
    }
}
