using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.PrivacyPools.Entrypoint;
using Nethereum.PrivacyPools.Entrypoint.ContractDefinition;
using Nethereum.PrivacyPools.PrivacyPoolSimple;
using Nethereum.PrivacyPools.PrivacyPoolBase;
using Nethereum.Web3;
using Nethereum.ZkProofs.Groth16;
using Nethereum.ZkProofsVerifier.Abstractions;
using EntrypointWithdrawal = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.Withdrawal;
using EntrypointWithdrawProof = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.WithdrawProof;

namespace Nethereum.PrivacyPools.Relayer
{
    public class RelayerConfig
    {
        public string EntrypointAddress { get; set; } = "";
        public string PoolAddress { get; set; } = "";
        public string FeeReceiverAddress { get; set; } = "";
        public BigInteger MinWithdrawAmount { get; set; } = BigInteger.Zero;
        public BigInteger MaxGasPrice { get; set; } = BigInteger.Zero;
        public BigInteger BaseFeeBps { get; set; } = BigInteger.Zero;
        public BigInteger RelayGasEstimate { get; set; } = 650_000;
    }

    public class PrivacyPoolRelayer
    {
        private readonly IWeb3 _web3;
        private readonly EntrypointService _entrypoint;
        private readonly PrivacyPoolSimpleService _pool;
        private readonly PrivacyPoolProofVerifier _verifier;
        private readonly IRelayRequestStore _requestStore;
        private readonly RelayerConfig _config;

        public BigInteger Scope { get; private set; }
        public RelayerConfig Config => _config;

        public PrivacyPoolRelayer(
            IWeb3 web3,
            RelayerConfig config,
            PrivacyPoolProofVerifier verifier,
            IRelayRequestStore requestStore = null)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _requestStore = requestStore ?? new InMemoryRelayRequestStore();
            _entrypoint = new EntrypointService(web3, config.EntrypointAddress);
            _pool = new PrivacyPoolSimpleService(web3, config.PoolAddress);
        }

        public async Task InitializeAsync()
        {
            Scope = await _pool.ScopeQueryAsync();
        }

        public async Task<RelayResult> HandleRelayRequestAsync(RelayRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var record = await _requestStore.CreateAsync(request);

            try
            {
                var validationError = await ValidateRequestAsync(request);
                if (validationError != null)
                {
                    await _requestStore.UpdateStatusAsync(record.Id, RelayRequestStatus.Failed, error: validationError);
                    return RelayResult.Failure(record.Id, validationError);
                }

                await _requestStore.UpdateStatusAsync(record.Id, RelayRequestStatus.Validated);

                var txHash = await BroadcastRelayAsync(request);

                await _requestStore.UpdateStatusAsync(record.Id, RelayRequestStatus.Broadcasted, transactionHash: txHash);
                return RelayResult.Success(record.Id, txHash);
            }
            catch (Exception ex)
            {
                var error = $"Relay failed: {ex.Message}";
                await _requestStore.UpdateStatusAsync(record.Id, RelayRequestStatus.Failed, error: error);
                return RelayResult.Failure(record.Id, error);
            }
        }

        public async Task<string> ValidateRequestAsync(RelayRequest request)
        {
            if (string.IsNullOrEmpty(request.Processooor))
                return "Missing processooor address";

            if (!string.Equals(request.Processooor, _config.EntrypointAddress, StringComparison.OrdinalIgnoreCase))
                return $"Processooor mismatch: expected {_config.EntrypointAddress}, got {request.Processooor}";

            if (request.Scope != Scope)
                return $"Scope mismatch: expected {Scope}, got {request.Scope}";

            if (string.IsNullOrEmpty(request.ProofJson) || string.IsNullOrEmpty(request.PublicSignalsJson))
                return "Missing proof or public signals";

            var signals = WithdrawProofSignals.FromArray(
                ZkProofs.ZkProofResult.ParsePublicSignals(request.PublicSignalsJson));

            if (_config.MinWithdrawAmount > BigInteger.Zero && signals.WithdrawnValue < _config.MinWithdrawAmount)
                return $"Withdrawn value {signals.WithdrawnValue} below minimum {_config.MinWithdrawAmount}";

            var expectedContext = ComputeExpectedContext(request);
            if (signals.Context != expectedContext)
                return "Context mismatch: proof context does not match withdrawal parameters";

            var onChainRoot = await _pool.CurrentRootQueryAsync();
            if (signals.StateRoot != onChainRoot)
                return $"State root mismatch: proof root {signals.StateRoot} does not match on-chain root {onChainRoot}";

            var proofResult = _verifier.VerifyWithdrawalProof(request.ProofJson, signals);
            if (!proofResult.IsValid)
                return $"Proof verification failed: {proofResult.Error}";

            var nullifierSpent = await _pool.NullifierHashesQueryAsync(signals.ExistingNullifierHash);
            if (nullifierSpent)
                return "Nullifier already spent";

            if (_config.MaxGasPrice > BigInteger.Zero)
            {
                var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
                if (gasPrice.Value > _config.MaxGasPrice)
                    return $"Gas price {gasPrice.Value} exceeds maximum {_config.MaxGasPrice}";
            }

            return null;
        }

        private BigInteger ComputeExpectedContext(RelayRequest request)
        {
            var withdrawal = new EntrypointWithdrawal
            {
                Processooor = request.Processooor,
                Data = request.WithdrawalData
            };
            return WithdrawalContextHelper.ComputeContext(withdrawal, Scope);
        }

        private async Task<string> BroadcastRelayAsync(RelayRequest request)
        {
            var signals = WithdrawProofSignals.FromArray(
                ZkProofs.ZkProofResult.ParsePublicSignals(request.PublicSignalsJson));

            var parsedProof = Groth16ProofConverter.ParseProofJson(request.ProofJson);
            var onChainProof = PrivacyPoolProofConverter.ToWithdrawProof(parsedProof, signals);

            var withdrawal = new EntrypointWithdrawal
            {
                Processooor = request.Processooor,
                Data = request.WithdrawalData
            };

            var relayFunction = new RelayFunction
            {
                Withdrawal = withdrawal,
                Proof = onChainProof,
                Scope = Scope,
                Gas = _config.RelayGasEstimate
            };

            var receipt = await _entrypoint.RelayRequestAndWaitForReceiptAsync(relayFunction);
            if (receipt.HasErrors() == true)
                throw new Exception($"Relay transaction reverted. Status: {receipt.Status?.Value}");

            return receipt.TransactionHash;
        }

        public async Task<RelayRequestRecord> GetRequestStatusAsync(string requestId)
        {
            return await _requestStore.GetAsync(requestId);
        }

        public RelayDetailsResponse GetDetails()
        {
            return new RelayDetailsResponse
            {
                EntrypointAddress = _config.EntrypointAddress,
                PoolAddress = _config.PoolAddress,
                FeeBps = _config.BaseFeeBps,
                MinWithdrawAmount = _config.MinWithdrawAmount,
                FeeReceiverAddress = _config.FeeReceiverAddress,
                MaxGasPrice = _config.MaxGasPrice
            };
        }
    }

    public class RelayDetailsResponse
    {
        public string EntrypointAddress { get; set; } = "";
        public string PoolAddress { get; set; } = "";
        public BigInteger FeeBps { get; set; }
        public BigInteger MinWithdrawAmount { get; set; }
        public string FeeReceiverAddress { get; set; } = "";
        public BigInteger MaxGasPrice { get; set; }
    }
}
