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
            return From(TransactionFactory.CreateTransaction(rlpEncoded), sender, block, executionState);
        }

        public static TransactionExecutionContext From(
            ISignedTransaction signedTx, string sender,
            IBlockEnvironment block,
            ExecutionStateService executionState)
        {
            var isContractCreation = signedTx.IsContractCreation();

            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Transaction,
                Sender = sender,
                To = isContractCreation ? null : signedTx.GetReceiverAddress(),
                Data = signedTx.GetData() ?? new byte[0],
                Value = signedTx.GetValue(),
                GasLimit = signedTx.GetGasLimit(),
                GasPrice = signedTx.GetMaxFeePerGas(),
                Nonce = signedTx.GetNonce(),
                IsContractCreation = isContractCreation,
                TransactionType = signedTx.TransactionType
            };

            SetFeeMarket(ctx, signedTx);
            SetBlobFields(ctx, signedTx);
            SetDeclaredChainId(ctx, signedTx);
            SetAuthorisationList(ctx, signedTx);
            SetAccessList(ctx, signedTx);
            SetBlockEnvironment(ctx, block, executionState);
            return ctx;
        }

        private static void SetFeeMarket(TransactionExecutionContext ctx, ISignedTransaction signedTx)
        {
            if (!signedTx.IsFeeMarketCapable()) return;

            ctx.IsEip1559 = true;
            ctx.MaxFeePerGas = signedTx.GetMaxFeePerGas();
            ctx.MaxPriorityFeePerGas = signedTx.GetMaxPriorityFeePerGas();
        }

        private static void SetBlobFields(TransactionExecutionContext ctx, ISignedTransaction signedTx)
        {
            if (!(signedTx is Transaction4844 tx4844)) return;

            ctx.IsType3Transaction = true;
            ctx.MaxFeePerBlobGas = tx4844.MaxFeePerBlobGas ?? EvmUInt256.Zero;
            if (tx4844.BlobVersionedHashes == null) return;

            ctx.BlobVersionedHashes = new List<string>(tx4844.BlobVersionedHashes.Count);
            foreach (var h in tx4844.BlobVersionedHashes)
                ctx.BlobVersionedHashes.Add(h.ToHex(true));
        }

        private static void SetDeclaredChainId(TransactionExecutionContext ctx, ISignedTransaction signedTx)
        {
            if (signedTx.DeclaresItsOwnChainId())
                ctx.DeclaredChainId = signedTx.GetChainId();
        }

        private static void SetAuthorisationList(TransactionExecutionContext ctx, ISignedTransaction signedTx)
        {
            if (signedTx is Transaction7702 tx7702)
                ctx.AuthorisationList = tx7702.AuthorisationList;
        }

        private static void SetAccessList(TransactionExecutionContext ctx, ISignedTransaction signedTx)
        {
            ctx.AccessList = AccessListEntry.From(signedTx.GetAccessList());
        }

        private static void SetBlockEnvironment(
            TransactionExecutionContext ctx,
            IBlockEnvironment block,
            ExecutionStateService executionState)
        {
            ctx.BlockNumber = block.BlockNumber;
            ctx.Timestamp = block.Timestamp;
            ctx.Coinbase = block.Coinbase;
            ctx.BaseFee = block.BaseFee;
            ctx.Difficulty = block.Difficulty;
            ctx.BlockGasLimit = block.BlockGasLimit;
            ctx.ChainId = block.ChainId;
            ctx.SlotNumber = block.SlotNumber.HasValue ? new EvmUInt256(block.SlotNumber.Value) : EvmUInt256.Zero;
            ctx.ExcessBlobGas = block.ExcessBlobGas;
            ctx.ExecutionState = executionState;
        }
    }
}
