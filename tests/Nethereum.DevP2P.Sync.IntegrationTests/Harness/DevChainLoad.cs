using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Chain.TestData;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.IntegrationTests.Harness
{
    public static class DevChainLoad
    {
        private const string StorageContractInitCode =
            "0x" +
            "6001600055" +
            "6002600155" +
            "6003600255" +
            "600a600c600039" +
            "600a6000f3" +
            "60016000556000";

        private const int GasPerHolder = 25_000;

        private const int HoldersPerTransaction = 15_000;

        public sealed class LoadResult
        {
            public BigInteger Height { get; set; }
            public int Transactions { get; set; }
            public List<string> ContractAddresses { get; } = new List<string>();
            public List<string> TouchedAccounts { get; } = new List<string>();
            public int ExtraAccounts { get; set; }
            public string TokenContract { get; set; }
            public int Holders { get; set; }
        }

        public static async Task<LoadResult> GenerateAsync(
            DevChainNode node,
            DevChainProducer producer,
            int blocks,
            int transfersPerBlock = 2,
            int contracts = 3,
            int extraAccounts = 0,
            int largeContractSlots = 0)
        {
            var result = new LoadResult();
            var chainId = (int)node.ChainId;
            var sender = ChainAccounts.Sender;
            var second = ChainAccounts.SecondSender;

            await node.FundAccountAsync(sender.Address, ChainAccounts.DefaultBalance);
            await node.FundAccountAsync(second.Address, ChainAccounts.DefaultBalance);
            result.TouchedAccounts.Add(sender.Address);
            result.TouchedAccounts.Add(second.Address);

            for (var i = 0; i < extraAccounts; i++)
            {
                var filler = FillerAddress(i);
                await node.FundAccountAsync(filler, 1_000_000 + i);
            }
            result.ExtraAccounts = extraAccounts;

            var nonce = BigInteger.Zero;

            for (var i = 0; i < contracts; i++)
            {
                var deploy = DevChainTransactions.SignEip1559(
                    sender.PrivateKey, chainId, to: "", nonce: nonce, value: BigInteger.Zero,
                    gasLimit: 1_000_000, data: StorageContractInitCode);

                await node.TxPool.AddAsync(deploy);
                nonce += 1;
                result.Transactions++;

                var produced = await producer.ProduceAsync();
                result.ContractAddresses.Add(ContractAddressOf(sender.Address, nonce - 1));
                result.Height = produced.Header.BlockNumber;
            }

            var secondNonce = BigInteger.Zero;
            for (var block = 0; block < blocks; block++)
            {
                for (var t = 0; t < transfersPerBlock; t++)
                {
                    var transfer = DevChainTransactions.SignEip1559(
                        sender.PrivateKey, chainId, second.Address, nonce, value: 1_000 + t);
                    await node.TxPool.AddAsync(transfer);
                    nonce += 1;
                    result.Transactions++;

                    var back = DevChainTransactions.SignEip1559(
                        second.PrivateKey, chainId, ChainAccounts.Recipient.Address, secondNonce, value: 500);
                    await node.TxPool.AddAsync(back);
                    secondNonce += 1;
                    result.Transactions++;
                }

                var produced = await producer.ProduceAsync();
                result.Height = produced.Header.BlockNumber;
            }

            // A USDC-shaped account: one contract holding a very large storage trie. Snap treats an
            // owner this big as a large contract (CreateLargeContract) and splits it into storage
            // subtasks, a path a handful of three-slot contracts never reaches.
            // A real ERC20, deployed and driven like any user would: airdrop to build holders, then
            // move value between existing holders so the state CHANGES rather than only grows. Every
            // call emits Transfer, so receipts, logs and the bloom carry the same work an explorer or
            // indexer would later read back.
            if (largeContractSlots > 0)
            {
                var deploy = DevChainTransactions.SignEip1559(
                    sender.PrivateKey, chainId, to: "", nonce: nonce, value: BigInteger.Zero,
                    gasLimit: 3_000_000, data: LoadTestToken.Bytecode);
                await node.TxPool.AddAsync(deploy);
                var token = ContractAddressOf(sender.Address, nonce);
                nonce += 1;
                result.Transactions++;
                await producer.ProduceAsync();

                var minted = 0;
                while (minted < largeContractSlots)
                {
                    var batch = Math.Min(HoldersPerTransaction, largeContractSlots - minted);
                    var airdrop = new AirdropFunction
                    {
                        StartIndex = minted,
                        Count = batch,
                        Amount = 1_000
                    };

                    await node.TxPool.AddAsync(DevChainTransactions.SignEip1559(
                        sender.PrivateKey, chainId, token, nonce, value: BigInteger.Zero,
                        gasLimit: (batch * GasPerHolder) + 500_000,
                        data: airdrop.GetCallData().ToHex(true)));

                    nonce += 1;
                    result.Transactions++;
                    var block = await producer.ProduceAsync();
                    result.Height = block.Header.BlockNumber;
                    minted += batch;
                }

                result.TokenContract = token.ToLowerInvariant();
                result.Holders = minted;
            }

            result.TouchedAccounts.Add(ChainAccounts.Recipient.Address);
            return result;
        }

        public static byte[] BalanceSlotValue(int slot)
        {
            var value = new byte[32];
            value[28] = (byte)(slot >> 24);
            value[29] = (byte)(slot >> 16);
            value[30] = (byte)(slot >> 8);
            value[31] = (byte)(slot | 1);
            return value;
        }

        public static string FillerAddress(int index)
        {
            var bytes = new byte[20];
            bytes[0] = 0xF1;
            bytes[1] = (byte)(index >> 16);
            bytes[2] = (byte)(index >> 8);
            bytes[3] = (byte)index;
            return "0x" + Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(bytes);
        }

        private static string ContractAddressOf(string deployer, BigInteger nonce) =>
            Nethereum.Util.ContractUtils.CalculateContractAddress(deployer, nonce);
    }
}
