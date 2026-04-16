using Nethereum.EVM;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public static class StateTestToWitnessConverter
    {
        public static BlockWitnessData Convert(GeneralStateTest test, PostResult expected, HardforkName fork)
        {
            if (fork == HardforkName.Unspecified)
                throw new System.ArgumentException(
                    "fork must be a specific HardforkName, not Unspecified.", nameof(fork));
            var env = test.Env;
            var tx = test.Transaction;

            var sender = tx.Sender ?? GetSenderFromSecretKey(tx.SecretKey);

            var blockNumber = string.IsNullOrEmpty(env.CurrentNumber) ? 1L : (long)env.CurrentNumber.HexToBigInteger(false);
            var timestamp = string.IsNullOrEmpty(env.CurrentTimestamp) ? 0L : (long)env.CurrentTimestamp.HexToBigInteger(false);
            var blockGasLimit = string.IsNullOrEmpty(env.CurrentGasLimit) ? 0L : (long)env.CurrentGasLimit.HexToBigInteger(false);
            var baseFee = string.IsNullOrEmpty(env.CurrentBaseFee) ? 0L : (long)env.CurrentBaseFee.HexToBigInteger(false);
            var coinbase = string.IsNullOrEmpty(env.CurrentCoinbase) ? "0x0000000000000000000000000000000000000000" : env.CurrentCoinbase;

            byte[] difficulty;
            if (!string.IsNullOrEmpty(env.CurrentRandom))
                difficulty = env.CurrentRandom.HexToByteArray().PadTo32Bytes();
            else if (!string.IsNullOrEmpty(env.CurrentDifficulty))
                difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(env.CurrentDifficulty.HexToBigInteger(false)).ToBigEndian();
            else
                difficulty = new byte[32];

            var txRlpEncoded = !string.IsNullOrEmpty(expected.TxBytes)
                ? expected.TxBytes.HexToByteArray()
                : new byte[0];

            var witness = new BlockWitnessData
            {
                BlockNumber = blockNumber,
                Timestamp = timestamp,
                BaseFee = baseFee,
                BlockGasLimit = blockGasLimit,
                ChainId = 1,
                Coinbase = coinbase,
                Difficulty = difficulty,
                ParentHash = new byte[32],
                MixHash = new byte[32],
                Nonce = new byte[8],
                ComputePostStateRoot = true,
                Features = new BlockFeatureConfig
                {
                    Fork = fork,
                    MaxBlobsPerBlock = fork >= HardforkName.Prague ? 9 : 6
                },
                Transactions = new List<BlockWitnessTransaction>
                {
                    new BlockWitnessTransaction
                    {
                        From = sender,
                        RlpEncoded = txRlpEncoded
                    }
                },
                Accounts = new List<WitnessAccount>()
            };

            foreach (var preAccount in test.Pre)
            {
                var address = preAccount.Key;
                var account = preAccount.Value;

                var witnessAccount = new WitnessAccount
                {
                    Address = address,
                    Balance = EvmUInt256BigIntegerExtensions.FromBigInteger(
                        string.IsNullOrEmpty(account.Balance) ? BigInteger.Zero : account.Balance.HexToBigInteger(false)),
                    Nonce = string.IsNullOrEmpty(account.Nonce) ? 0L : (long)account.Nonce.HexToBigInteger(false),
                    Code = string.IsNullOrEmpty(account.Code) || account.Code == "0x" ? new byte[0] : account.Code.HexToByteArray(),
                    Storage = new List<WitnessStorageSlot>()
                };

                if (account.Storage != null)
                {
                    foreach (var storage in account.Storage)
                    {
                        witnessAccount.Storage.Add(new WitnessStorageSlot
                        {
                            Key = EvmUInt256BigIntegerExtensions.FromBigInteger(storage.Key.HexToBigInteger(false)),
                            Value = EvmUInt256.FromBigEndian(storage.Value.HexToByteArray().PadTo32Bytes())
                        });
                    }
                }

                witness.Accounts.Add(witnessAccount);
            }

            return witness;
        }

        private static string GetSenderFromSecretKey(string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey))
                return "0x0000000000000000000000000000000000000000";
            var key = new Nethereum.Signer.EthECKey(secretKey);
            return key.GetPublicAddress();
        }
    }
}
