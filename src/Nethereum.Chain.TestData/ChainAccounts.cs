using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Signer;

namespace Nethereum.Chain.TestData
{
    public sealed class ChainAccount
    {
        public ChainAccount(string name, string privateKey, BigInteger balance)
        {
            Name = name;
            PrivateKey = privateKey;
            Balance = balance;
            Address = new EthECKey(privateKey).GetPublicAddress();
        }

        public string Name { get; }
        public string PrivateKey { get; }
        public string Address { get; }
        public BigInteger Balance { get; }

        public override string ToString() => $"{Name} {Address}";
    }

    public static class ChainAccounts
    {
        public static readonly BigInteger OneEther = BigInteger.Parse("1000000000000000000");
        public static readonly BigInteger DefaultBalance = OneEther * 1000;

        public static readonly ChainAccount Operator = new ChainAccount(
            "operator", ChainRoster.First.PrivateKey, DefaultBalance);

        public static readonly ChainAccount Sender = new ChainAccount(
            "sender", ChainRoster.Second.PrivateKey, DefaultBalance);

        public static readonly ChainAccount SecondSender = new ChainAccount(
            "second-sender", ChainRoster.Third.PrivateKey, DefaultBalance);

        public static readonly ChainAccount Recipient = new ChainAccount(
            "recipient", ChainRoster.Fourth.PrivateKey, DefaultBalance);

        public static readonly ChainAccount Unfunded = new ChainAccount(
            "unfunded", ChainRoster.NeverFunded.PrivateKey, BigInteger.Zero);

        public static readonly IReadOnlyList<ChainAccount> All =
            new[] { Operator, Sender, SecondSender, Recipient, Unfunded };

        public static IReadOnlyList<ChainAccount> Funded =>
            All.Where(a => a.Balance > BigInteger.Zero).ToList();
    }
}
