using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.Chain.TestData
{
    public sealed class TransactionScenario
    {
        public TransactionScenario(
            string name,
            byte transactionType,
            string eip,
            Func<TransactionScenarioContext, ISignedTransaction> build,
            BigInteger intrinsicGas)
        {
            Name = name;
            TransactionType = transactionType;
            Eip = eip;
            Build = build;
            IntrinsicGas = intrinsicGas;
        }

        public string Name { get; }

        /// <summary>The EIP-2718 type byte; 0 for pre-2718 legacy.</summary>
        public byte TransactionType { get; }

        public string Eip { get; }

        public Func<TransactionScenarioContext, ISignedTransaction> Build { get; }

        public BigInteger IntrinsicGas { get; }

        public override string ToString() => Name;
    }

    public sealed class TransactionScenarioContext
    {
        public TransactionScenarioContext(BigInteger chainId, ChainAccount sender, string recipient, BigInteger nonce)
        {
            ChainId = chainId;
            Sender = sender;
            Recipient = recipient;
            Nonce = nonce;
        }

        public BigInteger ChainId { get; }
        public ChainAccount Sender { get; }
        public string Recipient { get; }
        public BigInteger Nonce { get; }
        public BigInteger Value { get; set; } = 1000;
    }

    public static class TransactionScenarios
    {
        public static readonly TransactionScenario Legacy = new TransactionScenario(
            "legacy", 0, "pre-EIP-2718 (EIP-155 replay protected)",
            ctx => Sign(new LegacyTransactionSigner().SignTransaction(
                ctx.Sender.PrivateKey, ctx.ChainId, ctx.Recipient, ctx.Value, ctx.Nonce,
                gasPrice: OneGwei, gasLimit: 21_000, data: "")),
            21_000);

        public static readonly TransactionScenario AccessList = new TransactionScenario(
            "eip2930-access-list", 1, "EIP-2930",
            ctx => Sign(new TypeTransactionSigner<Transaction2930>().SignTransaction(
                ctx.Sender.PrivateKey,
                new Transaction2930(
                    (EvmUInt256)ctx.ChainId, ctx.Nonce, OneGwei, 50_000,
                    ctx.Recipient, ctx.Value, "", new List<AccessListItem>()))),
            50_000);

        public static readonly TransactionScenario FeeMarket = new TransactionScenario(
            "eip1559-fee-market", 2, "EIP-1559",
            ctx => Sign(new Transaction1559Signer().SignTransaction(
                ctx.Sender.PrivateKey,
                new Transaction1559(
                    (EvmUInt256)ctx.ChainId, ctx.Nonce, OneGwei, OneGwei, 21_000,
                    ctx.Recipient, ctx.Value, "", new List<AccessListItem>()))),
            21_000);

        public static readonly TransactionScenario SetCode = new TransactionScenario(
            "eip7702-set-code", 4, "EIP-7702",
            ctx => Sign(new TypeTransactionSigner<Transaction7702>().SignTransaction(
                ctx.Sender.PrivateKey,
                new Transaction7702(
                    (EvmUInt256)ctx.ChainId, ctx.Nonce, OneGwei, OneGwei, 200_000,
                    ctx.Recipient, ctx.Value, "", new List<AccessListItem>(),
                    new List<Authorisation7702Signed>
                    {
                        new Authorisation7702Signer().SignAuthorisation(
                            ctx.Sender.PrivateKey,
                            new Authorisation7702
                            {
                                ChainId = (EvmUInt256)ctx.ChainId,
                                Address = ChainAccounts.Recipient.Address,
                                Nonce = (EvmUInt256)(ctx.Nonce + 1)
                            })
                    }))),
            200_000);

        public static readonly IReadOnlyList<TransactionScenario> All =
            new[] { Legacy, AccessList, FeeMarket, SetCode };

        public static IEnumerable<object[]> AsMemberData() => All.Select(s => new object[] { s.Name });

        public static TransactionScenario ByName(string name) =>
            All.FirstOrDefault(s => s.Name == name)
            ?? throw new ArgumentOutOfRangeException(nameof(name), name, "unknown transaction scenario");

        private static readonly BigInteger OneGwei = 1_000_000_000;

        private static ISignedTransaction Sign(string signedHex) =>
            TransactionFactory.CreateTransaction(signedHex);
    }
}
