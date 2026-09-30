using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8037AuthorizationApplicationTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string DelegateAddress = "0x3333333333333333333333333333333333333333";
        private const string TargetAddress = "0x4444444444444444444444444444444444444444";
        private const string FreshValueTransferTargetAddress = "0x5555555555555555555555555555555555555555";
        private const string SecondDelegateAddress = "0x7777777777777777777777777777777777777777";
        private const string ZeroAddress = "0x0000000000000000000000000000000000000000";

        private const string PrivateKey1 = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string PrivateKey2 = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        private static readonly string Authority1 = EthECKey.GetPublicAddress(PrivateKey1);
        private static readonly string Authority2 = EthECKey.GetPublicAddress(PrivateKey2);

        private static readonly byte[] RevertImmediately = new byte[] { 0x60, 0x00, 0x60, 0x00, 0xFD };

        private static readonly byte[] SstoreThenRevert = new byte[]
        {
            0x60, 0x01, 0x60, 0x00, 0x55,
            0x60, 0x00, 0x60, 0x00, 0xFD
        };

        private static Authorisation7702Signed SignAuthorization(string privateKeyHex, string delegateAddress, BigInteger nonce)
        {
            var key = new EthECKey(privateKeyHex);
            var auth = new Authorisation7702 { ChainId = 1, Address = delegateAddress, Nonce = nonce };
            var signer = new Authorisation7702Signer();
            return signer.SignAuthorisation(key, auth);
        }

        private static async Task<EIP7702TestNodeDataService> FundedNodeAsync(string address, BigInteger balance)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(address, balance);
            return node;
        }

        private static TransactionExecutionContext AuthTxCtx(
            ExecutionStateService executionState, List<Authorisation7702Signed> authList, long gasLimit) => new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = TargetAddress,
                Data = Array.Empty<byte>(),
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 0,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

        [Fact]
        public async Task Given_AuthorizationAppliesNewAccountAndAuthBase_AtAmsterdam_When_DispatchedFrameReverts_Then_AuthStateGasStaysConsumed_And_SstoreInsideRevertsIsRefunded()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(TargetAddress, SstoreThenRevert);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };
            var ctx = AuthTxCtx(executionState, authList, gasLimit: 2_000_000);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.False(result.Success);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS + GasConstants.EIP8037_AUTH_BASE_STATE_GAS, result.StateGasUsed);

            var authorityCode = await executionState.GetCodeAsync(Authority1);
            Assert.True(Eip7702DelegationUtils.IsDelegatedCode(authorityCode));
            Assert.Equal(DelegateAddress.ToLower(), Eip7702DelegationUtils.GetDelegateAddress(authorityCode).ToLower());

            var slotValue = await executionState.GetFromStorageAsync(TargetAddress, Nethereum.Util.EvmUInt256.Zero);
            Assert.True(slotValue == null || System.Linq.Enumerable.All(slotValue, b => b == 0));
        }

        [Fact]
        public async Task Given_AuthorizationChargeSpillsIntoExecutionGas_AtAmsterdam_When_DispatchedFrameReverts_Then_SpillStaysConsumed()
        {
            const long reservoirGrant = 50_000;
            const long authCharge = GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS + GasConstants.EIP8037_AUTH_BASE_STATE_GAS;
            const long expectedSpill = authCharge - reservoirGrant;

            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(TargetAddress, RevertImmediately);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };

            var gasLimit = GasConstants.EIP8037_TX_MAX_GAS_LIMIT + reservoirGrant;
            var ctx = AuthTxCtx(executionState, authList, gasLimit);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.False(result.Success);
            Assert.Equal(expectedSpill, ctx.StateGas.SpilledIntoExecution);
            Assert.Equal(reservoirGrant, ctx.StateGas.FromReservoir);
            Assert.Equal(authCharge, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_AuthorityAlreadyAlive_AtAmsterdam_Then_NewAccountNotCharged()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(TargetAddress, RevertImmediately);
            await node.SetBalanceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };
            var ctx = AuthTxCtx(executionState, authList, gasLimit: 2_000_000);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.Equal(GasConstants.EIP8037_AUTH_BASE_STATE_GAS, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_AuthorityAlreadyDelegatedToSameTarget_AtAmsterdam_Then_AuthBaseNotCharged()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(TargetAddress, RevertImmediately);
            await node.SetCodeAsync(Authority1, Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress));
            await node.SetNonceAsync(Authority1, 0);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };
            var ctx = AuthTxCtx(executionState, authList, gasLimit: 2_000_000);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.Equal(0, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_TwoAuthorizations_AtAmsterdam_When_SecondAuthorizationChargeOOGs_Then_FirstAuthorizationAlsoRolledBack()
        {
            const long oneAuthorityChargeSet = GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS
                + GasConstants.EIP8038_ACCOUNT_WRITE
                + GasConstants.EIP8037_AUTH_BASE_STATE_GAS;

            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(TargetAddress, RevertImmediately);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed>
            {
                SignAuthorization(PrivateKey1, DelegateAddress, 0),
                SignAuthorization(PrivateKey2, DelegateAddress, 0)
            };

            const long derivedAmsterdamAuthCost = 7816;
            var baseIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                Array.Empty<byte>(), isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: false);
            var intrinsic = baseIntrinsic + derivedAmsterdamAuthCost * 2;
            var gasLimit = intrinsic + oneAuthorityChargeSet + 1_000;

            var ctx = AuthTxCtx(executionState, authList, gasLimit);
            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.True(ctx.AuthPrepFailed);
            Assert.False(result.Success);
            Assert.Equal(ctx.IntrinsicExecutionGas + ctx.ExecutionGasGrant, result.GasUsed);
            Assert.Equal(0, result.StateGasUsed);

            var authority1Code = await executionState.GetCodeAsync(Authority1);
            Assert.False(Eip7702DelegationUtils.IsDelegatedCode(authority1Code));
        }

        [Fact]
        public async Task Given_AuthorizationChargesSpillAndAccountWrite_AtAmsterdam_When_TopLevelValueTransferToNonAliveAccountFollows_Then_NewAccountChargeForfeitsRatherThanOverspendingGrant()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };

            const long derivedAmsterdamAuthCost = 7816;
            var baseIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                Array.Empty<byte>(), isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: true);
            var intrinsic = baseIntrinsic + derivedAmsterdamAuthCost;
            var gasLimit = intrinsic + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS;

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = FreshValueTransferTargetAddress,
                Data = Array.Empty<byte>(),
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 1000,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

            var senderNonceBefore = await executionState.GetNonceAsync(SenderAddress);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, ctx.ExecutionGasGrant);
            Assert.Equal(0, ctx.StateGasReservoir);

            Assert.False(result.Success);
            Assert.Equal(ctx.IntrinsicExecutionGas + ctx.ExecutionGasGrant, result.GasUsed);

            Assert.Equal(0, ctx.StateGas.SpilledIntoExecution);
            Assert.Equal(0, ctx.PreDispatchExecutionGasCharged);
            Assert.Equal(0, result.StateGasUsed);

            var authorityCode = await executionState.GetCodeAsync(Authority1);
            Assert.False(Eip7702DelegationUtils.IsDelegatedCode(authorityCode));

            var senderNonceAfter = await executionState.GetNonceAsync(SenderAddress);
            Assert.Equal(senderNonceBefore + EvmUInt256.One, senderNonceAfter);

            var targetBalance = await executionState.GetTotalBalanceAsync(FreshValueTransferTargetAddress);
            Assert.Equal(Nethereum.Util.EvmUInt256.Zero, targetBalance);
        }

        [Fact]
        public async Task Given_SingleAuthorization_AtOsaka_Then_UsesFlatRefundModel_NoStateGas_NoCrossForkLeak()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(TargetAddress, RevertImmediately);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };
            var ctx = AuthTxCtx(executionState, authList, gasLimit: 2_000_000);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.Equal(0, result.StateGasUsed);
            var authorityCode = await executionState.GetCodeAsync(Authority1);
            Assert.True(Eip7702DelegationUtils.IsDelegatedCode(authorityCode));
        }

        private sealed class ThrowingOnNonceLookupStateReader : IStateReader
        {
            private readonly IStateReader _inner;
            private readonly string _throwForAddress;
            public ThrowingOnNonceLookupStateReader(IStateReader inner, string throwForAddress) { _inner = inner; _throwForAddress = throwForAddress; }

            public Task<Nethereum.Util.EvmUInt256> GetBalanceAsync(byte[] address) => _inner.GetBalanceAsync(address);
            public Task<Nethereum.Util.EvmUInt256> GetBalanceAsync(string address) => _inner.GetBalanceAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] address) => _inner.GetCodeAsync(address);
            public Task<byte[]> GetCodeAsync(string address) => _inner.GetCodeAsync(address);
            public Task<byte[]> GetStorageAtAsync(byte[] address, Nethereum.Util.EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);
            public Task<byte[]> GetStorageAtAsync(string address, Nethereum.Util.EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);
            public Task<Nethereum.Util.EvmUInt256> GetTransactionCountAsync(byte[] address) => _inner.GetTransactionCountAsync(address);
            public Task<Nethereum.Util.EvmUInt256> GetTransactionCountAsync(string address)
            {
                if (string.Equals(address, _throwForAddress, System.StringComparison.OrdinalIgnoreCase))
                    throw new EvmHostException("simulated storage fault mid-authorization");
                return _inner.GetTransactionCountAsync(address);
            }
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task<byte[]> GetBlockHashAsync(long blockNumber) => _inner.GetBlockHashAsync(blockNumber);
        }

        [Fact]
        public async Task Given_StateReaderThrowsHostFault_AtAmsterdam_When_MidAuthorization_Then_FaultPropagates_NotSkippedAsInvalid()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(TargetAddress, RevertImmediately);
            var throwingReader = new ThrowingOnNonceLookupStateReader(node, Authority1);
            var executionState = new ExecutionStateService(throwingReader);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };
            var ctx = AuthTxCtx(executionState, authList, gasLimit: 2_000_000);

            var executor = new TransactionExecutor(config);
            await Assert.ThrowsAsync<EvmHostException>(() => executor.ExecuteAsync(ctx));
        }


        private const string CoinbaseAddress = "0x6666666666666666666666666666666666666666";

        [Fact]
        public async Task Given_RecipientTopFrameChargeOOG_When_AuthorizationsApplied_Then_AllDelegationsRollBack()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };

            const long derivedAmsterdamAuthCost = 7816;
            var baseIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                Array.Empty<byte>(), isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: true);
            var intrinsic = baseIntrinsic + derivedAmsterdamAuthCost;
            var gasLimit = intrinsic + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS;

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = FreshValueTransferTargetAddress,
                Data = Array.Empty<byte>(),
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 1000,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = CoinbaseAddress,
                ExecutionState = executionState
            };

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.False(result.Success);

            var authorityCode = await executionState.GetCodeAsync(Authority1);
            Assert.False(Eip7702DelegationUtils.IsDelegatedCode(authorityCode));
            var authorityNonce = await executionState.GetNonceAsync(Authority1);
            Assert.Equal(EvmUInt256.Zero, authorityNonce);

            var targetBalance = await executionState.GetTotalBalanceAsync(FreshValueTransferTargetAddress);
            Assert.Equal(EvmUInt256.Zero, targetBalance);
        }

        [Fact]
        public async Task Given_DelegationResolutionAccessChargeOOG_When_AuthorizationsApplied_Then_AllDelegationsRollBack()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };

            const long derivedAmsterdamAuthCost = 7816;
            const long committedBeforeDelegationAccess = GasConstants.EIP8037_AUTH_BASE_STATE_GAS + GasConstants.EIP8038_ACCOUNT_WRITE;
            const long remainderUnderColdAccess = 1000;
            var baseIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                Array.Empty<byte>(), isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: false);
            var intrinsic = baseIntrinsic + derivedAmsterdamAuthCost;
            var gasLimit = intrinsic + committedBeforeDelegationAccess + remainderUnderColdAccess;

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = Authority1,
                Data = Array.Empty<byte>(),
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 0,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = CoinbaseAddress,
                ExecutionState = executionState
            };

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.False(result.Success);
            Assert.True(ctx.CodeResolutionFailed);

            var authorityCode = await executionState.GetCodeAsync(Authority1);
            Assert.False(Eip7702DelegationUtils.IsDelegatedCode(authorityCode));
            var authorityNonce = await executionState.GetNonceAsync(Authority1);
            Assert.Equal(EvmUInt256.Zero, authorityNonce);
        }

        [Fact]
        public async Task Given_MultipleAuthorizationsAndCallRecipientChargeOOGs_When_ExecutingTransaction_Then_EveryAuthorizationRollsBackNotJustTheLast()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(Authority1, 1);
            await node.SetBalanceAsync(Authority2, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed>
            {
                SignAuthorization(PrivateKey1, DelegateAddress, 0),
                SignAuthorization(PrivateKey2, DelegateAddress, 0)
            };

            const long derivedAmsterdamAuthCost = 7816;
            const long committedPerAuthority = GasConstants.EIP8037_AUTH_BASE_STATE_GAS + GasConstants.EIP8038_ACCOUNT_WRITE;
            const long remainderUnderNewAccount = 1000;
            var baseIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                Array.Empty<byte>(), isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: true);
            var intrinsic = baseIntrinsic + derivedAmsterdamAuthCost * 2;
            var gasLimit = intrinsic + committedPerAuthority * 2 + remainderUnderNewAccount;

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = FreshValueTransferTargetAddress,
                Data = Array.Empty<byte>(),
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 1000,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = CoinbaseAddress,
                ExecutionState = executionState
            };

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.False(result.Success);

            var authority1Code = await executionState.GetCodeAsync(Authority1);
            var authority2Code = await executionState.GetCodeAsync(Authority2);
            Assert.False(Eip7702DelegationUtils.IsDelegatedCode(authority1Code));
            Assert.False(Eip7702DelegationUtils.IsDelegatedCode(authority2Code));
        }

        [Fact]
        public async Task Given_TxWithAuthorizationAndCallRecipientChargeOOGs_When_ExecutingTransaction_Then_SenderNonceIncrementsAndGasFeeDebitSurvives()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };

            const long derivedAmsterdamAuthCost = 7816;
            var baseIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                Array.Empty<byte>(), isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: true);
            var intrinsic = baseIntrinsic + derivedAmsterdamAuthCost;
            var gasLimit = intrinsic + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS;

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = FreshValueTransferTargetAddress,
                Data = Array.Empty<byte>(),
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 1000,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = CoinbaseAddress,
                ExecutionState = executionState
            };

            var senderNonceBefore = await executionState.GetNonceAsync(SenderAddress);
            var senderBalanceBefore = await executionState.GetTotalBalanceAsync(SenderAddress);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.False(result.Success);
            Assert.Equal((long)gasLimit, result.GasUsed);

            var senderNonceAfter = await executionState.GetNonceAsync(SenderAddress);
            Assert.Equal(senderNonceBefore + EvmUInt256.One, senderNonceAfter);

            var senderBalanceAfter = await executionState.GetTotalBalanceAsync(SenderAddress);
            var expectedDebit = new EvmUInt256((ulong)gasLimit) * new EvmUInt256(100UL);
            Assert.Equal(senderBalanceBefore - expectedDebit, senderBalanceAfter);
        }

        [Fact]
        public async Task Given_TxWithAuthorizationAndSuccessfulRecipientCharge_When_ExecutingTransaction_Then_AuthorizationEffectsPersist()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };
            var ctx = AuthTxCtx(executionState, authList, gasLimit: 2_000_000);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success);

            var authorityCode = await executionState.GetCodeAsync(Authority1);
            Assert.True(Eip7702DelegationUtils.IsDelegatedCode(authorityCode));
            Assert.Equal(DelegateAddress.ToLower(), Eip7702DelegationUtils.GetDelegateAddress(authorityCode).ToLower());
            var authorityNonce = await executionState.GetNonceAsync(Authority1);
            Assert.Equal(EvmUInt256.One, authorityNonce);
        }

        [Fact]
        public async Task Given_CallRecipientChargeOOGWithAuthorizations_When_ExecutingTransaction_Then_StateGasReservoirAndPreDispatchExecutionGasChargedAreFullyReset()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };

            const long derivedAmsterdamAuthCost = 7816;
            var baseIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                Array.Empty<byte>(), isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: true);
            var intrinsic = baseIntrinsic + derivedAmsterdamAuthCost;
            var gasLimit = intrinsic + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS;

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = FreshValueTransferTargetAddress,
                Data = Array.Empty<byte>(),
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 1000,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = CoinbaseAddress,
                ExecutionState = executionState
            };

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.False(result.Success);

            Assert.Equal(ctx.StateGasReservoir, ctx.StateGas.ReservoirRemaining);
            Assert.Equal(0, ctx.StateGas.FromReservoir);
            Assert.Equal(0, ctx.StateGas.SpilledIntoExecution);
            Assert.Equal(0, ctx.PreDispatchExecutionGasCharged);
            Assert.Equal(0, result.StateGasUsed);
        }


        [Fact]
        public async Task Given_AnAuthorityDelegatedBeforeTheTransaction_When_OneTupleClearsItAndAnotherResets_Then_TheSecondDoesNotPayAuthBase()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(Authority1, Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress));
            await node.SetNonceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed>
            {
                SignAuthorization(PrivateKey1, ZeroAddress, 1),
                SignAuthorization(PrivateKey1, SecondDelegateAddress, 2)
            };
            var ctx = AuthTxCtx(executionState, authList, gasLimit: 2_000_000);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);

            Assert.Equal(0, result.StateGasUsed);

            var authorityCode = await executionState.GetCodeAsync(Authority1);
            Assert.True(Eip7702DelegationUtils.IsDelegatedCode(authorityCode));
            Assert.Equal(SecondDelegateAddress.ToLower(), Eip7702DelegationUtils.GetDelegateAddress(authorityCode).ToLower());
            var authorityNonce = await executionState.GetNonceAsync(Authority1);
            Assert.Equal(new EvmUInt256(3UL), authorityNonce);
        }

        [Fact]
        public async Task Given_TwoTuplesForOneAuthority_When_Applied_Then_BothNonceBumpsAreVisible()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(Authority1, Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress));
            await node.SetNonceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed>
            {
                SignAuthorization(PrivateKey1, ZeroAddress, 1),
                SignAuthorization(PrivateKey1, SecondDelegateAddress, 2)
            };

            const long derivedAmsterdamAuthCost = 7816;
            var baseIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                Array.Empty<byte>(), isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: false);
            var intrinsic = baseIntrinsic + derivedAmsterdamAuthCost * 2;
            var gasLimit = intrinsic + GasConstants.EIP8038_ACCOUNT_WRITE + 10_000;

            var ctx = AuthTxCtx(executionState, authList, gasLimit);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);
            Assert.False(ctx.AuthPrepFailed);

            var authorityNonce = await executionState.GetNonceAsync(Authority1);
            Assert.Equal(new EvmUInt256(3UL), authorityNonce);
        }

        [Fact]
        public async Task Given_AnAuthorityNotDelegatedBeforeTheTransaction_When_ATupleDelegatesIt_Then_ItPaysAuthBaseOnce()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };
            var ctx = AuthTxCtx(executionState, authList, gasLimit: 2_000_000);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);
            Assert.Equal(GasConstants.EIP8037_AUTH_BASE_STATE_GAS, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_AnAuthorityNotDelegatedBeforeTheTransaction_When_TwoTuplesBothSetDelegation_Then_AuthBaseChargedOnlyOnce()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(Authority1, 1);
            var executionState = new ExecutionStateService(node);

            var authList = new List<Authorisation7702Signed>
            {
                SignAuthorization(PrivateKey1, DelegateAddress, 0),
                SignAuthorization(PrivateKey1, SecondDelegateAddress, 1)
            };
            var ctx = AuthTxCtx(executionState, authList, gasLimit: 2_000_000);

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);

            Assert.Equal(GasConstants.EIP8037_AUTH_BASE_STATE_GAS, result.StateGasUsed);

            var authorityCode = await executionState.GetCodeAsync(Authority1);
            Assert.True(Eip7702DelegationUtils.IsDelegatedCode(authorityCode));
            Assert.Equal(SecondDelegateAddress.ToLower(), Eip7702DelegationUtils.GetDelegateAddress(authorityCode).ToLower());
            var authorityNonce = await executionState.GetNonceAsync(Authority1);
            Assert.Equal(new EvmUInt256(2UL), authorityNonce);
        }
    }
}
