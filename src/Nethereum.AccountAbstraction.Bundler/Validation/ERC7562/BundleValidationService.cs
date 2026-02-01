using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class BundleValidationResult
    {
        public bool IsValid { get; set; }
        public List<StorageConflict> Conflicts { get; } = new();
        public Dictionary<string, ERC7562ValidationResult> UserOpResults { get; } = new();
        public List<string> RejectedUserOpHashes { get; } = new();
        public string Error { get; set; }

        public static BundleValidationResult Success()
        {
            return new BundleValidationResult { IsValid = true };
        }

        public static BundleValidationResult Failed(string error)
        {
            return new BundleValidationResult { IsValid = false, Error = error };
        }
    }

    public class BundleValidationOptions
    {
        public bool RejectOnAnyViolation { get; set; } = true;
        public bool RejectOnStorageConflict { get; set; } = true;
        public bool AllowWriteWriteConflicts { get; set; } = false;
        public int MaxConcurrentSimulations { get; set; } = 4;
        public long BlockNumber { get; set; } = -1;
        public long Timestamp { get; set; } = -1;
        public string Coinbase { get; set; }
        public BigInteger ChainId { get; set; }
    }

    public class BundleUserOpInfo
    {
        public string UserOpHash { get; set; }
        public PackedUserOperationDTO UserOp { get; set; }
        public string EntryPoint { get; set; }
        public EntityInfo Sender { get; set; }
        public EntityInfo Factory { get; set; }
        public EntityInfo Paymaster { get; set; }
        public EntityInfo Aggregator { get; set; }
    }

    public class BundleValidationService
    {
        private readonly ERC7562SimulationService _simulationService;
        private readonly BundleStorageConflictDetector _conflictDetector;

        public BundleValidationService(INodeDataService nodeDataService, HardforkConfig hardforkConfig = null)
        {
            _simulationService = new ERC7562SimulationService(nodeDataService, hardforkConfig);
            _conflictDetector = new BundleStorageConflictDetector();
        }

        public BundleValidationService(ERC7562SimulationService simulationService)
        {
            _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
            _conflictDetector = new BundleStorageConflictDetector();
        }

        public async Task<BundleValidationResult> ValidateBundleAsync(
            IList<BundleUserOpInfo> userOps,
            BundleValidationOptions options = null)
        {
            options ??= new BundleValidationOptions();
            var result = new BundleValidationResult { IsValid = true };

            if (userOps == null || userOps.Count == 0)
            {
                return BundleValidationResult.Failed("Empty bundle");
            }

            var profiles = new List<UserOpStorageProfile>();
            var validationTasks = new List<Task<(string hash, ERC7562ValidationResult result, UserOpStorageProfile profile)>>();

            foreach (var opInfo in userOps)
            {
                validationTasks.Add(ValidateSingleUserOpAsync(opInfo, options));
            }

            var validationResults = await Task.WhenAll(validationTasks);

            foreach (var (hash, validationResult, profile) in validationResults)
            {
                result.UserOpResults[hash] = validationResult;

                if (!validationResult.IsValid)
                {
                    result.RejectedUserOpHashes.Add(hash);

                    if (options.RejectOnAnyViolation)
                    {
                        result.IsValid = false;
                    }
                }
                else
                {
                    profile.UserOpHash = hash;
                    profiles.Add(profile);
                }
            }

            if (profiles.Count > 1 && options.RejectOnStorageConflict)
            {
                var conflicts = _conflictDetector.DetectConflicts(profiles);

                foreach (var conflict in conflicts)
                {
                    if (conflict.Type == StorageConflictType.WriteWrite && options.AllowWriteWriteConflicts)
                    {
                        continue;
                    }

                    result.Conflicts.Add(conflict);

                    if (!result.RejectedUserOpHashes.Contains(conflict.UserOpHash2))
                    {
                        result.RejectedUserOpHashes.Add(conflict.UserOpHash2);
                    }
                }

                if (result.Conflicts.Count > 0)
                {
                    result.IsValid = false;
                }
            }

            return result;
        }

        public async Task<ERC7562ValidationResult> ValidateSingleUserOperationAsync(
            BundleUserOpInfo opInfo,
            BundleValidationOptions options = null)
        {
            options ??= new BundleValidationOptions();

            return await _simulationService.ValidateUserOperationAsync(
                opInfo.UserOp,
                opInfo.EntryPoint,
                opInfo.Sender,
                opInfo.Factory,
                opInfo.Paymaster,
                opInfo.Aggregator,
                options.BlockNumber,
                options.Timestamp,
                options.Coinbase,
                options.ChainId);
        }

        public async Task<List<StorageConflict>> CheckStorageConflictsAsync(
            IList<BundleUserOpInfo> userOps,
            BundleValidationOptions options = null)
        {
            options ??= new BundleValidationOptions();

            var profiles = new List<UserOpStorageProfile>();

            foreach (var opInfo in userOps)
            {
                var profile = await _simulationService.GetStorageProfileAsync(
                    opInfo.UserOp,
                    opInfo.EntryPoint,
                    opInfo.Sender,
                    opInfo.Factory,
                    opInfo.Paymaster,
                    options.BlockNumber,
                    options.Timestamp,
                    options.Coinbase,
                    options.ChainId);

                profile.UserOpHash = opInfo.UserOpHash;
                profiles.Add(profile);
            }

            return _conflictDetector.DetectConflicts(profiles);
        }

        private async Task<(string hash, ERC7562ValidationResult result, UserOpStorageProfile profile)> ValidateSingleUserOpAsync(
            BundleUserOpInfo opInfo,
            BundleValidationOptions options)
        {
            var validationTask = _simulationService.ValidateUserOperationAsync(
                opInfo.UserOp,
                opInfo.EntryPoint,
                opInfo.Sender,
                opInfo.Factory,
                opInfo.Paymaster,
                opInfo.Aggregator,
                options.BlockNumber,
                options.Timestamp,
                options.Coinbase,
                options.ChainId);

            var profileTask = _simulationService.GetStorageProfileAsync(
                opInfo.UserOp,
                opInfo.EntryPoint,
                opInfo.Sender,
                opInfo.Factory,
                opInfo.Paymaster,
                options.BlockNumber,
                options.Timestamp,
                options.Coinbase,
                options.ChainId);

            await Task.WhenAll(validationTask, profileTask);

            return (opInfo.UserOpHash, validationTask.Result, profileTask.Result);
        }
    }
}
