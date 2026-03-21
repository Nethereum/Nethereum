using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.PrivacyPools.Entrypoint;
using Nethereum.PrivacyPools.Entrypoint.ContractDefinition;
using Nethereum.PrivacyPools.PrivacyPoolBase;
using Nethereum.PrivacyPools.PrivacyPoolSimple;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using PoolDepositedEvent = Nethereum.PrivacyPools.PrivacyPoolBase.DepositedEventDTO;
using PoolWithdrawnEvent = Nethereum.PrivacyPools.PrivacyPoolBase.WithdrawnEventDTO;
using EntrypointWithdrawal = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.Withdrawal;
using DepositFunction = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.DepositFunction;
using Deposit1Function = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.Deposit1Function;

namespace Nethereum.PrivacyPools
{
    public class DepositResult
    {
        public PrivacyPoolCommitment Commitment { get; set; } = null!;
        public int LeafIndex { get; set; }
        public TransactionReceipt Receipt { get; set; } = null!;
    }

    public class RagequitResult
    {
        public TransactionReceipt Receipt { get; set; } = null!;
    }

    public class WithdrawalResult
    {
        public PrivacyPoolCommitment NewCommitment { get; set; } = null!;
        public TransactionReceipt Receipt { get; set; } = null!;
    }

    public class PrivacyPoolService
    {
        private readonly IWeb3 _web3;
        private readonly EntrypointService _entrypoint;
        private readonly PrivacyPoolSimpleService _pool;
        private readonly ICommitmentStore _commitmentStore;

        public BigInteger Scope { get; private set; }
        public string PoolAddress => _pool.ContractAddress;
        public string EntrypointAddress => _entrypoint.ContractAddress;

        public PrivacyPoolService(IWeb3 web3, string entrypointAddress, string poolAddress,
            ICommitmentStore commitmentStore = null)
        {
            _web3 = web3;
            _entrypoint = new EntrypointService(web3, entrypointAddress);
            _pool = new PrivacyPoolSimpleService(web3, poolAddress);
            _commitmentStore = commitmentStore;
        }

        public static PrivacyPoolService FromDeployment(IWeb3 web3, PrivacyPoolDeploymentResult deployment,
            ICommitmentStore commitmentStore = null)
        {
            return new PrivacyPoolService(web3,
                deployment.Entrypoint.ContractAddress,
                deployment.Pool.ContractAddress,
                commitmentStore);
        }

        public async Task InitializeAsync()
        {
            Scope = await _pool.ScopeQueryAsync();
        }

        public async Task<BigInteger> GetOnChainRootAsync()
        {
            return await _pool.CurrentRootQueryAsync();
        }

        public async Task<BigInteger> GetTreeSizeAsync()
        {
            return await _pool.CurrentTreeSizeQueryAsync();
        }

        public async Task<DepositResult> DepositAsync(BigInteger value, BigInteger? nullifier = null, BigInteger? secret = null)
        {
            BigInteger n, s, precommitment;
            if (nullifier.HasValue && secret.HasValue)
            {
                n = nullifier.Value;
                s = secret.Value;
                precommitment = PrivacyPoolCommitment.Create(value, BigInteger.Zero, n, s).Precommitment;
            }
            else
            {
                (n, s, precommitment) = PrivacyPoolCommitment.GenerateRandomPrecommitment();
            }

            var depositFunction = new DepositFunction
            {
                Precommitment = precommitment,
                AmountToSend = value
            };

            var receipt = await _entrypoint.DepositRequestAndWaitForReceiptAsync(depositFunction);
            if (receipt.HasErrors() == true)
                throw new Exception("Deposit transaction failed");

            var poolEvents = receipt.DecodeAllEvents<PoolDepositedEvent>();
            if (!poolEvents.Any())
                throw new Exception("No deposit event found");

            var evt = poolEvents.First().Event;
            var commitment = PrivacyPoolCommitment.Create(evt.Value, evt.Label, n, s);

            var leafIndex = -1;
            if (_commitmentStore != null)
            {
                var treeSize = await _pool.CurrentTreeSizeQueryAsync();
                leafIndex = (int)treeSize - 1;
                await _commitmentStore.SaveAsync(commitment, leafIndex, _pool.ContractAddress);
            }

            return new DepositResult
            {
                Commitment = commitment,
                LeafIndex = leafIndex,
                Receipt = receipt
            };
        }

        public async Task<DepositResult> DepositERC20Async(string tokenAddress, BigInteger value,
            BigInteger? nullifier = null, BigInteger? secret = null)
        {
            BigInteger n, s, precommitment;
            if (nullifier.HasValue && secret.HasValue)
            {
                n = nullifier.Value;
                s = secret.Value;
                precommitment = PrivacyPoolCommitment.Create(value, BigInteger.Zero, n, s).Precommitment;
            }
            else
            {
                (n, s, precommitment) = PrivacyPoolCommitment.GenerateRandomPrecommitment();
            }

            var depositFunction = new Deposit1Function
            {
                Asset = tokenAddress,
                Value = value,
                Precommitment = precommitment
            };

            var receipt = await _entrypoint.DepositRequestAndWaitForReceiptAsync(depositFunction);
            if (receipt.HasErrors() == true)
                throw new Exception("ERC20 deposit transaction failed");

            var poolEvents = receipt.DecodeAllEvents<PoolDepositedEvent>();
            if (!poolEvents.Any())
                throw new Exception("No deposit event found");

            var evt = poolEvents.First().Event;
            var commitment = PrivacyPoolCommitment.Create(evt.Value, evt.Label, n, s);

            var leafIndex = -1;
            if (_commitmentStore != null)
            {
                var treeSize = await _pool.CurrentTreeSizeQueryAsync();
                leafIndex = (int)treeSize - 1;
                await _commitmentStore.SaveAsync(commitment, leafIndex, _pool.ContractAddress);
            }

            return new DepositResult
            {
                Commitment = commitment,
                LeafIndex = leafIndex,
                Receipt = receipt
            };
        }

        public async Task<TransactionReceipt> ApproveERC20Async(string tokenAddress, BigInteger amount)
        {
            return await _web3.Eth.ERC20.GetContractService(tokenAddress)
                .ApproveRequestAndWaitForReceiptAsync(EntrypointAddress, amount);
        }

        public async Task<RagequitResult> RagequitAsync(
            PrivacyPoolCommitment commitment,
            IPrivacyPoolProofProvider proofProvider)
        {
            var proofResult = await proofProvider.GenerateRagequitProofAsync(new RagequitWitnessInput
            {
                Nullifier = commitment.Nullifier,
                Secret = commitment.Secret,
                Value = commitment.Value,
                Label = commitment.Label
            });

            var ragequitProof = PrivacyPoolProofConverter.ToRagequitProof(
                proofResult.ProofJson, proofResult.Signals);

            var ragequitFunction = new RagequitFunction
            {
                Proof = ragequitProof,
                Gas = (BigInteger)5_000_000
            };

            var receipt = await _pool.RagequitRequestAndWaitForReceiptAsync(ragequitFunction);
            if (receipt.HasErrors() == true)
                throw new Exception("Ragequit transaction failed");

            if (_commitmentStore != null)
                await _commitmentStore.MarkSpentAsync(commitment.NullifierHash);

            return new RagequitResult { Receipt = receipt };
        }

        public async Task<WithdrawalResult> WithdrawAsync(
            PrivacyPoolCommitment commitment,
            int leafIndex,
            BigInteger withdrawnValue,
            string recipient,
            IPrivacyPoolProofProvider proofProvider,
            PoseidonMerkleTree stateTree,
            PoseidonMerkleTree aspTree,
            string aspIpfsCid = null)
        {
            if (stateTree == null) throw new ArgumentNullException(nameof(stateTree));
            if (aspTree == null) throw new ArgumentNullException(nameof(aspTree));

            var newCommitment = PrivacyPoolCommitment.CreateRandom(
                commitment.Value - withdrawnValue, commitment.Label);

            var stateMerkleProof = stateTree.GenerateInclusionProof(leafIndex);
            var stateSiblings = stateTree.GetProofSiblings(stateMerkleProof);
            var paddedStateSiblings = PadSiblings(stateSiblings, PrivacyPoolConstants.MAX_TREE_DEPTH);

            var aspMerkleProof = aspTree.GenerateInclusionProof(0);
            var aspSiblings = aspTree.GetProofSiblings(aspMerkleProof);
            var paddedASPSiblings = PadSiblings(aspSiblings, PrivacyPoolConstants.MAX_TREE_DEPTH);

            var aspRoot = aspTree.RootAsBigInteger;
            var updateReceipt = await _entrypoint.UpdateRootRequestAndWaitForReceiptAsync(
                aspRoot, aspIpfsCid ?? "");
            if (updateReceipt.HasErrors() == true)
                throw new Exception("ASP root update failed");

            var relayData = WithdrawalContextHelper.BuildRelayData(
                recipient, recipient, BigInteger.Zero);

            var withdrawal = new EntrypointWithdrawal
            {
                Processooor = _entrypoint.ContractAddress,
                Data = relayData
            };

            var context = WithdrawalContextHelper.ComputeContext(withdrawal, Scope);

            var witnessInput = new WithdrawalWitnessInput
            {
                ExistingValue = commitment.Value,
                ExistingNullifier = commitment.Nullifier,
                ExistingSecret = commitment.Secret,
                Label = commitment.Label,
                NewNullifier = newCommitment.Nullifier,
                NewSecret = newCommitment.Secret,
                WithdrawnValue = withdrawnValue,
                StateRoot = stateTree.RootAsBigInteger,
                StateTreeDepth = stateTree.Depth,
                StateSiblings = paddedStateSiblings,
                StateIndex = leafIndex,
                ASPRoot = aspRoot,
                ASPTreeDepth = aspTree.Depth,
                ASPSiblings = paddedASPSiblings,
                ASPIndex = 0,
                Context = context
            };

            var proofResult = await proofProvider.GenerateWithdrawalProofAsync(witnessInput);
            var onChainProof = PrivacyPoolProofConverter.ToWithdrawProof(
                proofResult.ProofJson, proofResult.Signals);

            var relayFunction = new RelayFunction
            {
                Withdrawal = withdrawal,
                Proof = onChainProof,
                Scope = Scope
            };

            var relayReceipt = await _entrypoint.RelayRequestAndWaitForReceiptAsync(relayFunction);
            if (relayReceipt.HasErrors() == true)
                throw new Exception("Relay transaction failed");

            if (_commitmentStore != null)
                await _commitmentStore.MarkSpentAsync(commitment.NullifierHash);

            return new WithdrawalResult
            {
                NewCommitment = newCommitment,
                Receipt = relayReceipt
            };
        }

        public async Task<WithdrawalResult> WithdrawDirectAsync(
            PrivacyPoolCommitment commitment,
            int leafIndex,
            BigInteger withdrawnValue,
            string recipient,
            IPrivacyPoolProofProvider proofProvider,
            PoseidonMerkleTree stateTree,
            PoseidonMerkleTree aspTree,
            string relayer = null,
            BigInteger? relayFeeBps = null)
        {
            if (stateTree == null) throw new ArgumentNullException(nameof(stateTree));
            if (aspTree == null) throw new ArgumentNullException(nameof(aspTree));

            var newCommitment = PrivacyPoolCommitment.CreateRandom(
                commitment.Value - withdrawnValue, commitment.Label);

            var stateMerkleProof = stateTree.GenerateInclusionProof(leafIndex);
            var stateSiblings = stateTree.GetProofSiblings(stateMerkleProof);
            var paddedStateSiblings = PadSiblings(stateSiblings, PrivacyPoolConstants.MAX_TREE_DEPTH);

            var aspMerkleProof = aspTree.GenerateInclusionProof(0);
            var aspSiblings = aspTree.GetProofSiblings(aspMerkleProof);
            var paddedASPSiblings = PadSiblings(aspSiblings, PrivacyPoolConstants.MAX_TREE_DEPTH);

            var aspRoot = aspTree.RootAsBigInteger;

            var callerAddress = _web3.TransactionManager.Account.Address;
            var relayData = WithdrawalContextHelper.BuildRelayData(
                recipient, relayer ?? callerAddress, relayFeeBps ?? BigInteger.Zero);

            var withdrawal = new PrivacyPoolBase.Withdrawal
            {
                Processooor = callerAddress,
                Data = relayData
            };

            var context = WithdrawalContextHelper.ComputeContext(
                new EntrypointWithdrawal { Processooor = callerAddress, Data = relayData },
                Scope);

            var witnessInput = new WithdrawalWitnessInput
            {
                ExistingValue = commitment.Value,
                ExistingNullifier = commitment.Nullifier,
                ExistingSecret = commitment.Secret,
                Label = commitment.Label,
                NewNullifier = newCommitment.Nullifier,
                NewSecret = newCommitment.Secret,
                WithdrawnValue = withdrawnValue,
                StateRoot = stateTree.RootAsBigInteger,
                StateTreeDepth = stateTree.Depth,
                StateSiblings = paddedStateSiblings,
                StateIndex = leafIndex,
                ASPRoot = aspRoot,
                ASPTreeDepth = aspTree.Depth,
                ASPSiblings = paddedASPSiblings,
                ASPIndex = 0,
                Context = context
            };

            var proofResult = await proofProvider.GenerateWithdrawalProofAsync(witnessInput);
            var onChainProof = PrivacyPoolProofConverter.ToWithdrawProof(
                proofResult.ProofJson, proofResult.Signals);

            var poolProof = new PrivacyPoolBase.WithdrawProof
            {
                PA = onChainProof.PA,
                PB = onChainProof.PB,
                PC = onChainProof.PC,
                PubSignals = onChainProof.PubSignals
            };

            var withdrawReceipt = await _pool.WithdrawRequestAndWaitForReceiptAsync(
                withdrawal, poolProof);
            if (withdrawReceipt.HasErrors() == true)
                throw new Exception("Direct withdrawal transaction failed");

            if (_commitmentStore != null)
                await _commitmentStore.MarkSpentAsync(commitment.NullifierHash);

            return new WithdrawalResult
            {
                NewCommitment = newCommitment,
                Receipt = withdrawReceipt
            };
        }

        private static BigInteger[] PadSiblings(BigInteger[] siblings, int targetLength)
        {
            var padded = new BigInteger[targetLength];
            for (int i = 0; i < siblings.Length && i < targetLength; i++)
                padded[i] = siblings[i];
            return padded;
        }
    }
}
