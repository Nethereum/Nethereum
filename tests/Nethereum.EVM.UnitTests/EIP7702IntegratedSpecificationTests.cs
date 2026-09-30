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
using Nethereum.Util;
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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

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
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var existingDelegationCode = CreateDelegationCode(DELEGATE_ADDRESS);
            await _nodeDataService.SetCodeAsync(authorityAddress, existingDelegationCode);
            await _nodeDataService.SetBalanceAsync(SENDER_ADDRESS, BigInteger.Parse("10000000000000000000"));
            await _nodeDataService.SetBalanceAsync(authorityAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetNonceAsync(authorityAddress, 1);

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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.NotNull(authorityAccountState.Code);
            Assert.Empty(authorityAccountState.Code);
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "NonceValidation")]
        public async Task Given_AuthorizationWithWrongNonce_When_Processed_Then_AuthorizationSkipped()
        {
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.True(authorityAccountState.Code == null || authorityAccountState.Code.Length == 0);
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "ChainIdValidation")]
        public async Task Given_AuthorizationWithWrongChainId_When_Processed_Then_AuthorizationSkipped()
        {
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var auth = new Authorisation7702
            {
                ChainId = 5,
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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.True(authorityAccountState.Code == null || authorityAccountState.Code.Length == 0);
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "ChainIdValidation")]
        public async Task Given_AuthorizationWithZeroChainId_When_ProcessedOnAnyChain_Then_AuthorizationApplied()
        {
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var auth = new Authorisation7702
            {
                ChainId = 0,
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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 137,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

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
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var delegateCode = "604260005260206000F3".HexToByteArray();
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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

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
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth1, signedAuth2 },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.Equal((ulong?)6, authorityAccountState.Nonce);
        }

        #endregion

        #region Delegation to Existing Contract Integration Tests

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "ExistingCodeCheck")]
        public async Task Given_EOAWithExistingCode_When_AuthorizationAttempted_Then_AuthorizationSkipped()
        {
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var existingCode = "60006000F3".HexToByteArray();
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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

            var authorityAccountState = executionState.CreateOrGetAccountExecutionState(authorityAddress);
            Assert.Equal(existingCode.Length, authorityAccountState.Code.Length);
            Assert.NotEqual(0xef, authorityAccountState.Code[0]);
        }

        [Fact]
        [Trait("Category", "EIP7702-Integration")]
        [Trait("Spec", "DelegationUpdate")]
        public async Task Given_EOAWithExistingDelegation_When_NewAuthorizationProcessed_Then_DelegationUpdated()
        {
            var authorityKey = new EthECKey(AUTHORITY_PRIVATE_KEY);
            var authorityAddress = authorityKey.GetPublicAddress();

            var oldDelegate = "0x1111111111111111111111111111111111111111";
            var existingDelegationCode = CreateDelegationCode(oldDelegate);
            await _nodeDataService.SetCodeAsync(authorityAddress, existingDelegationCode);
            await _nodeDataService.SetNonceAsync(authorityAddress, 1);

            var auth = new Authorisation7702
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
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
                AuthorisationList = new List<Authorisation7702Signed> { signedAuth },
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 1000000000,
                ChainId = 1,
                Coinbase = COINBASE_ADDRESS,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var result = await _executor.ExecuteAsync(ctx);

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

    public class EIP7702TestNodeDataService : IStateReader
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

        public Task<EvmUInt256> GetBalanceAsync(string address)
        {
            _balances.TryGetValue(address, out var balance);
            return Task.FromResult(EvmUInt256BigIntegerExtensions.FromBigInteger(balance));
        }

        public Task<EvmUInt256> GetBalanceAsync(byte[] address)
        {
            return GetBalanceAsync("0x" + address.ToHex());
        }

        public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position)
        {
            var positionBig = position.ToBigInteger();
            if (_storage.TryGetValue(address, out var slots))
            {
                if (slots.TryGetValue(positionBig, out var value))
                {
                    return Task.FromResult(value);
                }
            }
            return Task.FromResult(new byte[32]);
        }

        public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position)
        {
            return GetStorageAtAsync("0x" + address.ToHex(), position);
        }

        public Task<EvmUInt256> GetTransactionCountAsync(string address)
        {
            _nonces.TryGetValue(address, out var nonce);
            return Task.FromResult(EvmUInt256BigIntegerExtensions.FromBigInteger(nonce));
        }

        public Task<EvmUInt256> GetTransactionCountAsync(byte[] address)
        {
            return GetTransactionCountAsync("0x" + address.ToHex());
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

        public Task<byte[]> GetBlockHashAsync(long blockNumber)
        {
            return Task.FromResult(new byte[32]);
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            return Task.FromResult(_code.ContainsKey(address) || _balances.ContainsKey(address));
        }
    }
}
