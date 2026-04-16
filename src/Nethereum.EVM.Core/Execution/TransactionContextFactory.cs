using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public static class TransactionContextFactory
    {
        public static TransactionExecutionContext FromBlockWitnessTransaction(
            BlockWitnessTransaction wtx,
            BlockWitnessData block,
            ExecutionStateService executionState)
        {
            var ctx = FromRlpEncoded(wtx.RlpEncoded, wtx.From, block, executionState);
            ctx.AuthorisationAuthorities = wtx.AuthorisationAuthorities;
            return ctx;
        }

        public static TransactionExecutionContext FromRlpEncoded(
            byte[] rlpEncoded, string sender,
            BlockWitnessData block,
            ExecutionStateService executionState)
        {
            var signedTx = TransactionFactory.CreateTransaction(rlpEncoded);

            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Transaction,
                Sender = sender,
                To = signedTx.GetReceiverAddress() ?? "",
                Data = signedTx.GetData() ?? new byte[0],
                Value = signedTx.GetValue(),
                GasLimit = (long)signedTx.GetGasLimit(),
                GasPrice = (long)signedTx.GetMaxFeePerGas(),
                Nonce = (long)signedTx.GetNonce(),
                IsContractCreation = signedTx.IsContractCreation(),
                EffectiveGasPrice = signedTx.GetMaxFeePerGas()
            };

            // EIP-1559
            if (signedTx is Transaction1559 tx1559)
            {
                ctx.IsEip1559 = true;
                ctx.MaxFeePerGas = tx1559.MaxFeePerGas ?? EvmUInt256.Zero;
                ctx.MaxPriorityFeePerGas = tx1559.MaxPriorityFeePerGas ?? EvmUInt256.Zero;
                var priority = ctx.MaxPriorityFeePerGas < (ctx.MaxFeePerGas - block.BaseFee)
                    ? ctx.MaxPriorityFeePerGas : (ctx.MaxFeePerGas - block.BaseFee);
                ctx.EffectiveGasPrice = block.BaseFee + priority;
            }

            // EIP-7702: carry through the authorisation list for in-guest processing
            // (canonical signature validation must be done by the witness generator;
            // the zkVM executor reads pre-recovered authorities from
            // TransactionExecutionContext.AuthorisationAuthorities).
            if (signedTx is Transaction7702 t7702Auth)
            {
                ctx.IsType4Transaction = true;
                ctx.AuthorisationList = t7702Auth.AuthorisationList;
            }

            // Access lists
            List<AccessListItem> rawAL = null;
            if (signedTx is Transaction1559 t1559al) rawAL = t1559al.AccessList;
            else if (signedTx is Transaction2930 t2930) rawAL = t2930.AccessList;
            else if (signedTx is Transaction7702 t7702) rawAL = t7702.AccessList;

            if (rawAL != null)
            {
                ctx.AccessList = new List<AccessListEntry>();
                foreach (var item in rawAL)
                {
                    var keys = new List<string>();
                    if (item.StorageKeys != null)
                        foreach (var k in item.StorageKeys)
                            keys.Add(k.ToHex(true));
                    ctx.AccessList.Add(new AccessListEntry { Address = item.Address, StorageKeys = keys });
                }
            }

            SetBlockContext(ctx, block, executionState);
            return ctx;
        }

        private static void SetBlockContext(
            TransactionExecutionContext ctx,
            BlockWitnessData block,
            ExecutionStateService executionState)
        {
            ctx.BlockNumber = block.BlockNumber;
            ctx.Timestamp = block.Timestamp;
            ctx.Coinbase = block.Coinbase;
            ctx.BaseFee = block.BaseFee;
            // Post-merge: DIFFICULTY opcode returns PREVRANDAO (from MixHash)
            var difficulty = block.Difficulty != null ? EvmUInt256.FromBigEndian(block.Difficulty) : EvmUInt256.Zero;
            if (difficulty.IsZero && block.MixHash != null && block.MixHash.Length > 0)
                ctx.Difficulty = EvmUInt256.FromBigEndian(block.MixHash);
            else
                ctx.Difficulty = difficulty;
            ctx.BlockGasLimit = block.BlockGasLimit;
            ctx.ChainId = block.ChainId;
            ctx.ExecutionState = executionState;
        }
    }
}
