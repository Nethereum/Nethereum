using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.PrivacyPools.Processing;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.ZkProofs;

namespace Nethereum.PrivacyPools
{
    public class PrivacyPool
    {
        public PrivacyPoolAccount Account { get; }
        public PrivacyPoolService Pool { get; }
        public PrivacyPoolProofVerifier Verifier { get; private set; }

        public IWeb3 Web3 { get; }
        public string EntrypointAddress { get; }
        public string PoolAddress { get; }
        public BigInteger Scope { get; private set; }
        public List<PoolAccount> PoolAccounts { get; } = new List<PoolAccount>();

        public PrivacyPool(IWeb3 web3, string entrypointAddress, string poolAddress, string mnemonic,
            string mnemonicPassword = "", ICommitmentStore commitmentStore = null)
        {
            Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            EntrypointAddress = entrypointAddress;
            PoolAddress = poolAddress;

            Account = new PrivacyPoolAccount(mnemonic, mnemonicPassword);
            Pool = new PrivacyPoolService(web3, entrypointAddress, poolAddress, commitmentStore);
        }

        public PrivacyPool(IWeb3 web3, string entrypointAddress, string poolAddress,
            PrivacyPoolAccount account, ICommitmentStore commitmentStore = null)
        {
            Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            EntrypointAddress = entrypointAddress;
            PoolAddress = poolAddress;

            Account = account ?? throw new ArgumentNullException(nameof(account));
            Pool = new PrivacyPoolService(web3, entrypointAddress, poolAddress, commitmentStore);
        }

        public static PrivacyPool FromDeployment(IWeb3 web3, PrivacyPoolDeploymentResult deployment,
            string mnemonic, string mnemonicPassword = "", ICommitmentStore commitmentStore = null)
        {
            return new PrivacyPool(web3,
                deployment.Entrypoint.ContractAddress,
                deployment.Pool.ContractAddress,
                mnemonic, mnemonicPassword, commitmentStore);
        }

        public static PrivacyPool FromDeployment(IWeb3 web3, PrivacyPoolDeploymentResult deployment,
            PrivacyPoolAccount account, ICommitmentStore commitmentStore = null)
        {
            return new PrivacyPool(web3,
                deployment.Entrypoint.ContractAddress,
                deployment.Pool.ContractAddress,
                account, commitmentStore);
        }

        public async Task InitializeAsync()
        {
            await Pool.InitializeAsync();
            Scope = Pool.Scope;
        }

        public List<PoolAccount> RecoverAccounts(
            IEnumerable<PoolDepositEventData> deposits,
            IEnumerable<PoolWithdrawalEventData> withdrawals,
            IEnumerable<PoolRagequitEventData> ragequits,
            IEnumerable<PoolLeafEventData> leafInserts,
            int maxConsecutiveMisses = 10)
        {
            var recovered = PrivacyPoolAccountRecovery.RecoverAccounts(
                Account, Scope, deposits, withdrawals, ragequits, leafInserts, maxConsecutiveMisses);
            PoolAccounts.Clear();
            PoolAccounts.AddRange(recovered);
            return recovered;
        }

        public List<PoolAccount> GetSpendableAccounts()
        {
            return PrivacyPoolAccountRecovery.GetSpendable(PoolAccounts);
        }

        public (BigInteger Nullifier, BigInteger Secret, BigInteger Precommitment) CreateDepositSecrets(BigInteger depositIndex)
        {
            var (nullifier, secret) = Account.CreateDepositSecrets(Scope, depositIndex);
            var precommitment = Account.ComputePrecommitment(nullifier, secret);
            return (nullifier, secret, precommitment);
        }

        public async Task<DepositResult> DepositAsync(BigInteger value, BigInteger depositIndex)
        {
            var (nullifier, secret) = Account.CreateDepositSecrets(Scope, depositIndex);
            return await Pool.DepositAsync(value, nullifier, secret);
        }

        public async Task<DepositResult> DepositERC20Async(string tokenAddress, BigInteger value, BigInteger depositIndex)
        {
            var (nullifier, secret) = Account.CreateDepositSecrets(Scope, depositIndex);
            return await Pool.DepositERC20Async(tokenAddress, value, nullifier, secret);
        }

        public async Task<TransactionReceipt> ApproveERC20Async(string tokenAddress, BigInteger amount)
        {
            return await Pool.ApproveERC20Async(tokenAddress, amount);
        }

        public async Task<RagequitResult> RagequitAsync(PoolAccount poolAccount, IPrivacyPoolProofProvider proofProvider)
        {
            if (poolAccount == null) throw new ArgumentNullException(nameof(poolAccount));
            return await Pool.RagequitAsync(poolAccount.LatestCommitment.Commitment, proofProvider);
        }

        public async Task<WithdrawalResult> WithdrawDirectAsync(
            PoolAccount poolAccount,
            BigInteger withdrawnValue,
            string recipient,
            IPrivacyPoolProofProvider proofProvider,
            PoseidonMerkleTree stateTree,
            PoseidonMerkleTree aspTree)
        {
            if (poolAccount == null) throw new ArgumentNullException(nameof(poolAccount));
            return await Pool.WithdrawDirectAsync(
                poolAccount.LatestCommitment.Commitment,
                poolAccount.LatestCommitment.LeafIndex,
                withdrawnValue, recipient, proofProvider, stateTree, aspTree);
        }

        public async Task<SyncResult> SyncFromChainAsync(
            BigInteger? fromBlock = null,
            PoseidonMerkleTree existingStateTree = null)
        {
            var repository = new InMemoryPrivacyPoolRepository();
            var stateTree = existingStateTree ?? new PoseidonMerkleTree();

            var currentBlock = await Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var processingService = new PrivacyPoolLogProcessingService(Web3, PoolAddress);
            var processor = processingService.CreateProcessor(repository, stateTree);
            await processor.ExecuteAsync(currentBlock,
                startAtBlockNumberIfNotProcessed: fromBlock ?? BigInteger.Zero);

            var deposits = await repository.GetDepositsAsync();
            var withdrawals = await repository.GetWithdrawalsAsync();
            var ragequits = await repository.GetRagequitsAsync();
            var leaves = await repository.GetLeavesAsync();

            var recovered = RecoverAccounts(deposits, withdrawals, ragequits, leaves);

            var asp = CreateASPTreeService();
            asp.BuildFromDeposits(deposits);

            return new SyncResult
            {
                PoolAccounts = recovered,
                StateTree = stateTree,
                ASPTree = asp,
                Deposits = deposits,
                LastBlockProcessed = currentBlock
            };
        }

        public ASPTreeService CreateASPTreeService()
        {
            return new ASPTreeService(
                new Entrypoint.EntrypointService(Web3, EntrypointAddress));
        }

        public ASPTreeService CreateASPTreeFromDeposits(IEnumerable<PoolDepositEventData> deposits)
        {
            var asp = CreateASPTreeService();
            asp.BuildFromDeposits(deposits);
            return asp;
        }

        public PrivacyPoolProofProvider CreateProofProvider(IZkProofProvider zkProvider, ICircuitArtifactSource artifactSource)
        {
            return new PrivacyPoolProofProvider(zkProvider, artifactSource);
        }

        public void SetVerifier(string withdrawalVkJson, string ragequitVkJson = null)
        {
            Verifier = new PrivacyPoolProofVerifier(withdrawalVkJson, ragequitVkJson);
        }
    }
}
