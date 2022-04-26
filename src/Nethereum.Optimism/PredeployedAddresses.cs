namespace Nethereum.Optimism
{
    /**
    * Predeploys are Solidity contracts that are injected into the initial L2 state and provide
    * various useful functions.
    */
    public class PredeployedAddresses
    {
        public const string OVM_L2ToL1MessagePasser = "0x4200000000000000000000000000000000000000";
        public const string OVM_DeployerWhitelist = "0x4200000000000000000000000000000000000002";
        public const string L2CrossDomainMessenger = "0x4200000000000000000000000000000000000007";
        public const string OVM_GasPriceOracle = "0x420000000000000000000000000000000000000F";
        public const string L2StandardBridge = "0x4200000000000000000000000000000000000010";
        public const string OVM_SequencerFeeVault = "0x4200000000000000000000000000000000000011";
        public const string L2StandardTokenFactory = "0x4200000000000000000000000000000000000012";
        public const string OVM_L1BlockNumber = "0x4200000000000000000000000000000000000013";
    }
}