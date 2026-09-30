using System;
using System.Security.Cryptography;

namespace Nethereum.CoreChain.Forks
{
    public static class Eip7685Constants
    {
        public const string SystemAddress = Nethereum.Util.AddressUtil.SYSTEM_ADDRESS;

        /// <summary>
        /// EIP-7685: <c>"For a block with no requests data, the requests_hash
        /// is simply sha256("")."</c>
        /// </summary>
        public static readonly byte[] EmptyRequestsHash = SHA256.HashData(Array.Empty<byte>());
    }
}
