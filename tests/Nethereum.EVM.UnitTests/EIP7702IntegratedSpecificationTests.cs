using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EIP7702IntegratedSpecificationTests
    {
        private readonly TransactionExecutor _executor;
        private readonly EIP7702TestNodeDataService _nodeDataService;
        private readonly HardforkConfig _config;

        private const string AUTHORITY_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SENDER_PRIVATE_KEY = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
        private const string SENDER_ADDRESS = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
        private const string DELEGATE_ADDRESS = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const string ZERO_ADDRESS = "0x0000000000000000000000000000000000000000";
        private const string COINBASE_ADDRESS = "0x0000000000000000000000000000000000000000";

        public EIP7702IntegratedSpecificationTests()
        {
            _config = HardforkConfig.Prague;
            _executor = new TransactionExecutor(_config);
            _nodeDataService = new EIP7702TestNodeDataService();
        }

        #region Authorization Processing Integration Tests

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "AuthorizationProcessing")]
        public async Task Given_Type4Transaction_When_AuthorizationProcessed_Then_DelegationCodeSetOnEOA()
        {
            // GIVEN: An EOA with a signed authorization delegating to a contract address
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var auth = new Authorisation7702
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));

            var executionState = new ExecutionStateService(_nodeDataService);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Authority account should have delegation code (0xef0100 + delegate address)
            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.NotNull(authorityAccountState.Code);
            Assert.Equal(23, authorityAccountState.Code.Length);
            Assert.Equal(0xef, authorityAccountState.Code[0]);
            Assert.Equal(0x01, authorityAccountState.Code[1]);
            Assert.Equal(0x00, authorityAccountState.Code[2]);

            var extractedAddress = new byte[20];
            Array.Copy(authorityAccountState.Code, 3, extractedAddress, 0, 20);
            Assert.Equal(DELEGATE_ADDRESS.ToLowerInvariant(), ("0x" + extractedAddress.ToHex()).ToLowerInvariant());
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "AuthorizationRemoval")]
        public async Task Given_AuthorizationWithZeroAddress_When_Processed_Then_DelegationCodeCleared()
        {
            // GIVEN: An EOA with existing delegation code
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            // First, set existing delegation code
            var existingDelegationCode = CreateDelegationCode(DELEGATE_ADDRESS);
            await _nodeDataService.SetCodeAsync(authorityAddress, existingDelegationCode);
            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetNonceAsync(authorityAddress, 1);

            // Create authorization with zero address (removal)
            var auth = new Authorisation7702
            {
                ChainId = 1,
                Address = ZERO_ADDRESS,
                Nonce = 1
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            var executionState = new ExecutionStateService(_nodeDataService);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Authority account should have empty code (delegation removed)
            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.NotNull(authorityAccountState.Code);
            Assert.Empty(authorityAccountState.Code);
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "NonceValidation")]
        public async Task Given_AuthorizationWithWrongNonce_When_Processed_Then_AuthorizationSkipped()
        {
            // GIVEN: An EOA with nonce 0, but authorization specifies nonce 5
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var auth = new Authorisation7702
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 5 // Wrong nonce - authority has nonce 0
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetNonceAsync(authorityAddress, 0);

            var executionState = new ExecutionStateService(_nodeDataService);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Authority account should NOT have delegation code (nonce mismatch)
            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.True(authorityAccountState.Code == null || authorityAccountState.Code.Length == 0);
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "ChainIdValidation")]
        public async Task Given_AuthorizationWithWrongChainId_When_Processed_Then_AuthorizationSkipped()
        {
            // GIVEN: Transaction on chain 1, but authorization specifies chain 5
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var auth = new Authorisation7702
            {
                ChainId = 5, // Wrong chain - transaction is on chain 1
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));

            var executionState = new ExecutionStateService(_nodeDataService);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1, // Transaction is on chain 1
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Authority account should NOT have delegation code (chain mismatch)
            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.True(authorityAccountState.Code == null || authorityAccountState.Code.Length == 0);
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "ChainIdValidation")]
        public async Task Given_AuthorizationWithZeroChainId_When_ProcessedOnAnyChain_Then_AuthorizationApplied()
        {
            // GIVEN: Authorization with chain_id = 0 (universal) on chain 137 (Polygon)
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var auth = new Authorisation7702
            {
                ChainId = 0, // Universal - valid on any chain
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));

            var executionState = new ExecutionStateService(_nodeDataService);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 137, // Polygon - but auth has chain_id = 0
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Authority account should have delegation code (universal auth)
            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.NotNull(authorityAccountState.Code);
            Assert.Equal(23, authorityAccountState.Code.Length);
            Assert.Equal(0xef, authorityAccountState.Code[0]);
        }

        #endregion

        #region Delegation Code Execution Integration Tests

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "DelegationExecution")]
        public async Task Given_DelegatedEOA_When_Called_Then_DelegateCodeExecuted()
        {
            // GIVEN: An EOA with delegation code pointing to a contract that returns a specific value
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            // Deploy delegate contract that returns 0x42 (66 decimal)
            // PUSH1 0x42 PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var delegateCode = "604260005260206000F3".HexToByteArray();
            await _nodeDataService.SetCodeAsync(DELEGATE_ADDRESS, delegateCode);

            // Create signed authorization
            var auth = new Authorisation7702
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));

            var executionState = new ExecutionStateService(_nodeDataService);
            await PreloadBalanceAsync(executionState, SENDER_ADDRESS);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress, // Calling the EOA that will be delegated
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Execution should succeed and return value from delegate
            Assert.True(result.Success, $"Transaction failed: {result.Error}");
            Assert.NotNull(result.ReturnData);

            if (result.ReturnData.Length >= 32)
            {
                var returnValue = new BigInteger(result.ReturnData.Skip(result.ReturnData.Length - 1).Take(1).Reverse().ToArray());
                Assert.Equal(0x42, returnValue);
            }
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "DelegationExecution")]
        public async Task Given_DelegatedEOA_When_DelegateAccessesMSGSENDER_Then_OriginalCallerReturned()
        {
            // GIVEN: An EOA delegating to contract that returns CALLER (msg.sender)
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            // Contract that returns CALLER: CALLER PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var delegateCode = "3360005260206000F3".HexToByteArray();
            await _nodeDataService.SetCodeAsync(DELEGATE_ADDRESS, delegateCode);

            var auth = new Authorisation7702
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));

            var executionState = new ExecutionStateService(_nodeDataService);
            await PreloadBalanceAsync(executionState, SENDER_ADDRESS);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Return data should contain the original sender address
            Assert.True(result.Success, $"Transaction failed: {result.Error}");
            Assert.NotNull(result.ReturnData);
            Assert.True(result.ReturnData.Length >= 20);

            var returnedAddress = "0x" + result.ReturnData.Skip(12).Take(20).ToArray().ToHex();
            Assert.Equal(SENDER_ADDRESS.ToLowerInvariant(), returnedAddress.ToLowerInvariant());
        }

        #endregion

        #region Gas Cost Integration Tests

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "GasCosts")]
        public async Task Given_Type4Transaction_When_HasAuthorizationList_Then_IntrinsicGasIncludesAuthCost()
        {
            // GIVEN: A Type 4 transaction with 2 authorizations
            var authorityKey1 = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityKey2 = new EthECKey(SENDER_PRIVATE_KEY);

            var auth1 = new Authorisation7702 { ChainId = 1, Address = DELEGATE_ADDRESS, Nonce = 0 };
            var auth2 = new Authorisation7702 { ChainId = 1, Address = DELEGATE_ADDRESS, Nonce = 0 };

            var signer = new Authorisation7702Signer();
            var signedAuth1 = signer.SignAuthorisation(authorityKey1, auth1);
            var signedAuth2 = signer.SignAuthorisation(authorityKey2, auth2);

            var authorityAddress1 = authorityKey1.GetPublicAddress();
            var authorityAddress2 = authorityKey2.GetPublicAddress();

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress1, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress2, BigInteger.Parse("1000000000000000000"));

            var executionState = new ExecutionStateService(_nodeDataService);
            await PreloadBalanceAsync(executionState, SENDER_ADDRESS);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress1,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth1, signedAuth2 },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Gas used should include authorization costs
            // Base intrinsic: 21000
            // Authorization cost: 2 * 12500 = 25000
            // Minimum expected: 46000
            Assert.True(result.GasUsed >= 46000,
                $"Expected gas >= 46000 (21000 + 2*12500), got {result.GasUsed}");
        }

        #endregion

        #region Nonce Increment Integration Tests

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "NonceIncrement")]
        public async Task Given_SuccessfulAuthorization_When_Processed_Then_AuthorityNonceIncremented()
        {
            // GIVEN: An EOA with nonce 5
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            await _nodeDataService.SetNonceAsync(authorityAddress, 5);

            var auth = new Authorisation7702
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 5
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));

            var executionState = new ExecutionStateService(_nodeDataService);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Authority nonce should be incremented to 6
            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.Equal(6ul, authorityAccountState.Nonce);
        }

        #endregion

        #region Delegation to Existing Contract Integration Tests

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "ExistingCodeCheck")]
        public async Task Given_EOAWithExistingCode_When_AuthorizationAttempted_Then_AuthorizationSkipped()
        {
            // GIVEN: An account that already has non-delegation code
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            // Set existing (non-delegation) code
            var existingCode = "60006000F3".HexToByteArray(); // Simple contract, not delegation
            await _nodeDataService.SetCodeAsync(authorityAddress, existingCode);

            var auth = new Authorisation7702
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));

            var executionState = new ExecutionStateService(_nodeDataService);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Authorization should be skipped - existing code unchanged
            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.Equal(existingCode.Length, authorityAccountState.Code.Length);
            Assert.NotEqual(0xef, authorityAccountState.Code[0]); // Not delegation code
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "DelegationUpdate")]
        public async Task Given_EOAWithExistingDelegation_When_NewAuthorizationProcessed_Then_DelegationUpdated()
        {
            // GIVEN: An EOA with existing delegation code pointing to address A
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var oldDelegate = "0x1111111111111111111111111111111111111111";
            var existingDelegationCode = CreateDelegationCode(oldDelegate);
            await _nodeDataService.SetCodeAsync(authorityAddress, existingDelegationCode);
            await _nodeDataService.SetNonceAsync(authorityAddress, 1);

            // Create new authorization pointing to a different address
            var auth = new Authorisation7702
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS, // New delegate
                Nonce = 1
            };
            var signer = new Authorisation7702Signer();
            var signedAuth = signer.SignAuthorisation(authorityKey, auth);

            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));

            var executionState = new ExecutionStateService(_nodeDataService);

            var ctx = new TransactionExecutionContext
            {
                Sender = SENDER_ADDRESS,
                To = authorityAddress,
                Data = Array.Empty<byte>(),
                GasLimit = 100000,
                Value = 0,
                GasPrice = 1000000000,
                MaxFeePerGas = 1000000000,
                MaxPriorityFeePerGas = 100000000,
                Nonce = 0,
                IsType4Transaction = true,
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            // WHEN: Transaction is executed
            var result = await _executor.ExecuteAsync(ctx);

            // THEN: Delegation should be updated to new address
            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.NotNull(authorityAccountState.Code);
            Assert.Equal(23, authorityAccountState.Code.Length);

            var extractedAddress = new byte[20];
            Array.Copy(authorityAccountState.Code, 3, extractedAddress, 0, 20);
            Assert.Equal(DELEGATE_ADDRESS.ToLowerInvariant(), ("0x" + extractedAddress.ToHex()).ToLowerInvariant());
        }

        #endregion

        #region Helper Methods

        private static byte[] CreateDelegationCode(string address)
        {
            var code = new byte[23];
            code[0] = 0xef;
            code[1] = 0x01;
            code[2] = 0x00;
            var addressBytes = address.HexToByteArray();
            Array.Copy(addressBytes, 0, code, 3, 20);
            return code;
        }

        private async Task PreloadBalanceAsync(ExecutionStateService executionState, string address)
        {
            var account = executionState.CreateOrGetAccountExecutionState(address);
            var balance = await executionState.NodeDataService.GetBalanceAsync(address);
            account.Balance.SetInitialChainBalance(balance);
        }

        #endregion
    }

    public class EIP7702TestNodeDataService : INodeDataService
    {
        private readonly Dictionary<string, byte[]> _code = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BigInteger> _balances = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<BigInteger, byte[]>> _storage = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BigInteger> _nonces = new(StringComparer.OrdinalIgnoreCase);

        public Task<byte[]> GetCodeAsync(string address)
        {
            _code.TryGetValue(address, out var code);
            return Task.FromResult(code ?? Array.Empty<byte>());
        }

        public Task<byte[]> GetCodeAsync(byte[] address)
        {
            return GetCodeAsync("0x" + address.ToHex());
        }

        public Task<BigInteger> GetBalanceAsync(string address)
        {
            _balances.TryGetValue(address, out var balance);
            return Task.FromResult(balance);
        }

        public Task<BigInteger> GetBalanceAsync(byte[] address)
        {
            return GetBalanceAsync("0x" + address.ToHex());
        }

        public Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
        {
            if (_storage.TryGetValue(address, out var slots))
            {
                if (slots.TryGetValue(position, out var value))
                {
                    return Task.FromResult(value);
                }
            }
            return Task.FromResult(new byte[32]);
        }

        public Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position)
        {
            return GetStorageAtAsync("0x" + address.ToHex(), position);
        }

        public Task<BigInteger> GetTransactionCount(string address)
        {
            _nonces.TryGetValue(address, out var nonce);
            return Task.FromResult(nonce);
        }

        public Task<BigInteger> GetTransactionCount(byte[] address)
        {
            return GetTransactionCount("0x" + address.ToHex());
        }

        public Task SetCodeAsync(string address, byte[] code)
        {
            _code[address] = code;
            return Task.CompletedTask;
        }

        public Task SetBalanceAsync(string address, BigInteger balance)
        {
            _balances[address] = balance;
            return Task.CompletedTask;
        }

        public Task SetNonceAsync(string address, BigInteger nonce)
        {
            _nonces[address] = nonce;
            return Task.CompletedTask;
        }

        public Task SetStorageAsync(string address, BigInteger slot, byte[] value)
        {
            if (!_storage.TryGetValue(address, out var slots))
            {
                slots = new Dictionary<BigInteger, byte[]>();
                _storage[address] = slots;
            }
            slots[slot] = value;
            return Task.CompletedTask;
        }

        public Task<byte[]> GetBlockHashAsync(BigInteger blockNumber)
        {
            return Task.FromResult(new byte[32]);
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            return Task.FromResult(_code.ContainsKey(address) || _balances.ContainsKey(address));
        }
    }
}
