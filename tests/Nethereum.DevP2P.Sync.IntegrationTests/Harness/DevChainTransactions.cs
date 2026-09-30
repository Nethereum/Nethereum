using System.Collections.Generic;
using System.Numerics;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.IntegrationTests.Harness
{
    /// <summary>
    /// Signs real EIP-1559 transactions for the devchain harness so tests exercise the production
    /// admission + wire path (never a stub transaction). Reusable across E2E scenarios.
    /// </summary>
    public static class DevChainTransactions
    {
        public static string AddressOf(string privateKey)
            => new EthECKey(privateKey).GetPublicAddress().ToLowerInvariant();

        /// <summary>
        /// Sign a well-formed EIP-1559 transaction for <paramref name="chainId"/>. Defaults produce a plain
        /// funded value transfer (21000 gas, 1 gwei fees) that passes mempool admission when the sender is funded.
        /// </summary>
        public static ISignedTransaction SignEip1559(
            string privateKey,
            BigInteger chainId,
            string to,
            BigInteger nonce,
            BigInteger value,
            BigInteger gasLimit = default,
            BigInteger maxFeePerGas = default,
            BigInteger maxPriorityFeePerGas = default,
            string data = "")
        {
            if (gasLimit == default) gasLimit = 21_000;
            if (maxFeePerGas == default) maxFeePerGas = 1_000_000_000;
            if (maxPriorityFeePerGas == default) maxPriorityFeePerGas = 1_000_000_000;

            var tx = new Transaction1559(
                (EvmUInt256)chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit,
                to, value, data, new List<AccessListItem>());
            var signedHex = new Transaction1559Signer().SignTransaction(privateKey, tx);
            return TransactionFactory.CreateTransaction(signedHex);
        }

        /// <summary>Sign a pre-EIP-2718 legacy transaction (no type byte), EIP-155 replay protected.</summary>
        public static ISignedTransaction SignLegacy(
            string privateKey,
            BigInteger chainId,
            string to,
            BigInteger nonce,
            BigInteger value,
            BigInteger gasLimit = default,
            BigInteger gasPrice = default,
            string data = "")
        {
            if (gasLimit == default) gasLimit = 21_000;
            if (gasPrice == default) gasPrice = 1_000_000_000;

            var signedHex = new LegacyTransactionSigner()
                .SignTransaction(privateKey, chainId, to, value, nonce, gasPrice, gasLimit, data);
            return TransactionFactory.CreateTransaction(signedHex);
        }

        /// <summary>Sign an EIP-2930 (type 0x01) access-list transaction.</summary>
        public static ISignedTransaction SignEip2930(
            string privateKey,
            BigInteger chainId,
            string to,
            BigInteger nonce,
            BigInteger value,
            List<AccessListItem> accessList = null,
            BigInteger gasLimit = default,
            BigInteger gasPrice = default,
            string data = "")
        {
            if (gasLimit == default) gasLimit = 50_000;
            if (gasPrice == default) gasPrice = 1_000_000_000;

            var tx = new Transaction2930(
                (EvmUInt256)chainId, nonce, gasPrice, gasLimit,
                to, value, data, accessList ?? new List<AccessListItem>());
            var signedHex = new TypeTransactionSigner<Transaction2930>().SignTransaction(privateKey, tx);
            return TransactionFactory.CreateTransaction(signedHex);
        }

        /// <summary>
        /// Sign an EIP-7702 (type 0x04) set-code transaction delegating <paramref name="privateKey"/>'s
        /// account to <paramref name="delegateTo"/>.
        /// </summary>
        public static ISignedTransaction SignEip7702(
            string privateKey,
            BigInteger chainId,
            string to,
            BigInteger nonce,
            string delegateTo,
            BigInteger authorisationNonce,
            BigInteger value = default,
            BigInteger gasLimit = default,
            BigInteger maxFeePerGas = default,
            BigInteger maxPriorityFeePerGas = default,
            string data = "")
        {
            if (gasLimit == default) gasLimit = 200_000;
            if (maxFeePerGas == default) maxFeePerGas = 1_000_000_000;
            if (maxPriorityFeePerGas == default) maxPriorityFeePerGas = 1_000_000_000;

            var authorisation = new Authorisation7702Signer().SignAuthorisation(
                privateKey,
                new Authorisation7702
                {
                    ChainId = (EvmUInt256)chainId,
                    Address = delegateTo,
                    Nonce = (EvmUInt256)authorisationNonce
                });

            var tx = new Transaction7702(
                (EvmUInt256)chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit,
                to, value, data, new List<AccessListItem>(),
                new List<Authorisation7702Signed> { authorisation });
            var signedHex = new TypeTransactionSigner<Transaction7702>().SignTransaction(privateKey, tx);
            return TransactionFactory.CreateTransaction(signedHex);
        }
    }
}
