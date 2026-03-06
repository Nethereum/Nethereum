using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.AppChain.Policy.Bootstrap
{
    public class BootstrapPolicyConfig
    {
        public List<string> AllowedWriters { get; set; } = new List<string>();

        public List<string> AllowedAdmins { get; set; } = new List<string>();

        public BigInteger MaxCalldataBytes { get; set; } = 128_000;

        public BigInteger MaxLogBytes { get; set; } = 1_000_000;

        public BigInteger BlockGasLimit { get; set; } = 30_000_000;

        public string? SequencerAddress { get; set; }

        public string? L1PolicyContractAddress { get; set; }

        public string? L1RpcUrl { get; set; }

        public BigInteger L1ChainId { get; set; }

        public bool AutoMigrateWhenL1Available { get; set; } = false;

        public bool OpenWriterAccess { get; set; } = false;

        public bool OpenAdminAccess { get; set; } = false;

        public PolicyConfig ToPolicyConfig()
        {
            return new PolicyConfig
            {
                Enabled = true,
                PolicyContractAddress = L1PolicyContractAddress,
                TargetRpcUrl = L1RpcUrl,
                TargetChainId = L1ChainId,
                MaxCalldataBytes = MaxCalldataBytes,
                MaxLogBytes = MaxLogBytes,
                BlockGasLimit = BlockGasLimit,
                AllowedWriters = AllowedWriters.Count > 0 ? AllowedWriters : null,
                Epoch = 0
            };
        }

        public static BootstrapPolicyConfig OpenAccess()
        {
            return new BootstrapPolicyConfig
            {
                OpenWriterAccess = true,
                OpenAdminAccess = true
            };
        }

        public static BootstrapPolicyConfig WithWriters(params string[] writers)
        {
            var config = new BootstrapPolicyConfig();
            config.AllowedWriters.AddRange(writers);
            return config;
        }
    }
}
