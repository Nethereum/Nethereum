using System.Threading;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.ERC7579.Modules
{
    public static class NethereumAccountModuleExtensions
    {
        public static Task<string> InstallModuleAsync(
            this NethereumAccountService account,
            IModuleConfig config)
        {
            return account.InstallModuleRequestAsync(
                config.ModuleTypeId,
                config.ModuleAddress,
                config.GetInitData());
        }

        public static Task<TransactionReceipt> InstallModuleAndWaitForReceiptAsync(
            this NethereumAccountService account,
            IModuleConfig config,
            CancellationTokenSource cancellationToken = null)
        {
            return account.InstallModuleRequestAndWaitForReceiptAsync(
                config.ModuleTypeId,
                config.ModuleAddress,
                config.GetInitData(),
                cancellationToken);
        }

        public static Task<string> UninstallModuleAsync(
            this NethereumAccountService account,
            IModuleConfig config)
        {
            return account.UninstallModuleRequestAsync(
                config.ModuleTypeId,
                config.ModuleAddress,
                config.GetDeInitData());
        }

        public static Task<TransactionReceipt> UninstallModuleAndWaitForReceiptAsync(
            this NethereumAccountService account,
            IModuleConfig config,
            CancellationTokenSource cancellationToken = null)
        {
            return account.UninstallModuleRequestAndWaitForReceiptAsync(
                config.ModuleTypeId,
                config.ModuleAddress,
                config.GetDeInitData(),
                cancellationToken);
        }

        public static Task<bool> IsModuleInstalledAsync(
            this NethereumAccountService account,
            IModuleConfig config,
            byte[] additionalContext = null)
        {
            return account.IsModuleInstalledQueryAsync(
                config.ModuleTypeId,
                config.ModuleAddress,
                additionalContext ?? System.Array.Empty<byte>());
        }

        public static Task<string> InstallECDSAValidatorAsync(
            this NethereumAccountService account,
            string validatorAddress,
            string ownerAddress)
        {
            var config = ECDSAValidatorConfig.Create(validatorAddress, ownerAddress);
            return account.InstallModuleAsync(config);
        }

        public static Task<TransactionReceipt> InstallECDSAValidatorAndWaitForReceiptAsync(
            this NethereumAccountService account,
            string validatorAddress,
            string ownerAddress,
            CancellationTokenSource cancellationToken = null)
        {
            var config = ECDSAValidatorConfig.Create(validatorAddress, ownerAddress);
            return account.InstallModuleAndWaitForReceiptAsync(config, cancellationToken);
        }

        public static Task<string> InstallOwnableValidatorAsync(
            this NethereumAccountService account,
            string validatorAddress,
            int threshold,
            params string[] owners)
        {
            var config = OwnableValidatorConfig.Create(validatorAddress, threshold, owners);
            return account.InstallModuleAsync(config);
        }

        public static Task<TransactionReceipt> InstallOwnableValidatorAndWaitForReceiptAsync(
            this NethereumAccountService account,
            string validatorAddress,
            int threshold,
            string[] owners,
            CancellationTokenSource cancellationToken = null)
        {
            var config = OwnableValidatorConfig.Create(validatorAddress, threshold, owners);
            return account.InstallModuleAndWaitForReceiptAsync(config, cancellationToken);
        }

        public static Task<string> InstallSocialRecoveryAsync(
            this NethereumAccountService account,
            string moduleAddress,
            int threshold,
            params string[] guardians)
        {
            var config = SocialRecoveryConfig.Create(moduleAddress, threshold, guardians);
            return account.InstallModuleAsync(config);
        }

        public static Task<TransactionReceipt> InstallSocialRecoveryAndWaitForReceiptAsync(
            this NethereumAccountService account,
            string moduleAddress,
            int threshold,
            string[] guardians,
            CancellationTokenSource cancellationToken = null)
        {
            var config = SocialRecoveryConfig.Create(moduleAddress, threshold, guardians);
            return account.InstallModuleAndWaitForReceiptAsync(config, cancellationToken);
        }

        public static Task<string> InstallOwnableExecutorAsync(
            this NethereumAccountService account,
            string executorAddress,
            string ownerAddress)
        {
            var config = OwnableExecutorConfig.Create(executorAddress, ownerAddress);
            return account.InstallModuleAsync(config);
        }

        public static Task<TransactionReceipt> InstallOwnableExecutorAndWaitForReceiptAsync(
            this NethereumAccountService account,
            string executorAddress,
            string ownerAddress,
            CancellationTokenSource cancellationToken = null)
        {
            var config = OwnableExecutorConfig.Create(executorAddress, ownerAddress);
            return account.InstallModuleAndWaitForReceiptAsync(config, cancellationToken);
        }
    }
}
