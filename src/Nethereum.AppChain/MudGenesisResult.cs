using Nethereum.Mud.Contracts;

namespace Nethereum.AppChain
{
    public class MudGenesisResult
    {
        public string WorldAddress { get; set; } = "";
        public string WorldFactoryAddress { get; set; } = "";
        public string InitModuleAddress { get; set; } = "";
        public string AccessManagementSystemAddress { get; set; } = "";
        public string BalanceTransferSystemAddress { get; set; } = "";
        public string BatchCallSystemAddress { get; set; } = "";
        public string RegistrationSystemAddress { get; set; } = "";
        public string Create2FactoryAddress { get; set; } = "";
        public string DeployerAddress { get; set; } = "";

        public static MudGenesisResult FromWorldFactoryAddresses(
            WorldFactoryContractAddresses factoryAddresses,
            string worldAddress,
            string create2FactoryAddress,
            string deployerAddress)
        {
            return new MudGenesisResult
            {
                WorldAddress = worldAddress,
                WorldFactoryAddress = factoryAddresses.WorldFactoryAddress,
                InitModuleAddress = factoryAddresses.InitModuleAddress,
                AccessManagementSystemAddress = factoryAddresses.AccessManagementSystemAddress,
                BalanceTransferSystemAddress = factoryAddresses.BalanceTransferSystemAddress,
                BatchCallSystemAddress = factoryAddresses.BatchCallSystemAddress,
                RegistrationSystemAddress = factoryAddresses.RegistrationSystemAddress,
                Create2FactoryAddress = create2FactoryAddress,
                DeployerAddress = deployerAddress
            };
        }
    }
}
