using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession
{
    public class ERC20SpendingLimitBuilder
    {
        private readonly List<TokenLimit> _limits = new List<TokenLimit>();

        public ERC20SpendingLimitBuilder AddTokenLimit(string tokenAddress, BigInteger limit)
        {
            if (string.IsNullOrEmpty(tokenAddress))
                throw new ArgumentNullException(nameof(tokenAddress));

            if (limit <= 0)
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));

            _limits.Add(new TokenLimit
            {
                TokenAddress = tokenAddress,
                Limit = limit
            });
            return this;
        }

        public byte[] Build()
        {
            if (_limits.Count == 0)
                throw new InvalidOperationException("At least one token limit must be configured");

            var addresses = new List<string>();
            var limits = new List<BigInteger>();

            foreach (var limit in _limits)
            {
                addresses.Add(limit.TokenAddress);
                limits.Add(limit.Limit);
            }

            var abiEncoder = new ABIEncode();
            return abiEncoder.GetABIEncoded(
                new ABIValue("address[]", addresses.ToArray()),
                new ABIValue("uint256[]", limits.ToArray())
            );
        }

        public static byte[] SingleToken(string tokenAddress, BigInteger limit)
        {
            return new ERC20SpendingLimitBuilder()
                .AddTokenLimit(tokenAddress, limit)
                .Build();
        }

        private class TokenLimit
        {
            public string TokenAddress { get; set; }
            public BigInteger Limit { get; set; }
        }
    }
}
