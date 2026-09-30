using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.ABI.EIP712.Permit2
{
    /// <summary>
    /// The EIP-712 message the owner signs for a Permit2 SignatureTransfer <c>permitTransferFrom</c>.
    /// Unlike the on-chain function argument tuple <c>PermitTransferFrom</c> (in
    /// Nethereum.Contracts.Standards.Permit2, which omits it), the signed message MUST include the
    /// <c>spender</c> — Permit2 hashes the caller (<c>msg.sender</c>)
    /// as the spender, so a signature that leaves it out never recovers to the owner on-chain.
    /// EIP-712 type: <c>PermitTransferFrom(TokenPermissions permitted,address spender,uint256 nonce,uint256 deadline)</c>.
    /// </summary>
    [Struct("PermitTransferFrom")]
    public class PermitTransferFromWithSpender
    {
        [Parameter("tuple", "permitted", 1, "TokenPermissions")]
        public virtual TokenPermissions Permitted { get; set; }

        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }

        [Parameter("uint256", "nonce", 3)]
        public virtual BigInteger Nonce { get; set; }

        [Parameter("uint256", "deadline", 4)]
        public virtual BigInteger Deadline { get; set; }
    }

    [Struct("PermitBatchTransferFrom")]
    public class PermitBatchTransferFromWithSpender
    {
        [Parameter("tuple[]", "permitted", 1, "TokenPermissions[]")]
        public virtual List<TokenPermissions> Permitted { get; set; }

        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }

        [Parameter("uint256", "nonce", 3)]
        public virtual BigInteger Nonce { get; set; }

        [Parameter("uint256", "deadline", 4)]
        public virtual BigInteger Deadline { get; set; }
    }
}
