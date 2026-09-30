using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Interfaces
{
    public interface IAggregator
    {
        Task ValidateSignaturesAsync(
            PackedUserOperation[] userOps,
            byte[] signature);

        Task<byte[]> ValidateUserOpSignatureAsync(PackedUserOperation userOp);

        Task<byte[]> AggregateSignaturesAsync(PackedUserOperation[] userOps);
    }
}
