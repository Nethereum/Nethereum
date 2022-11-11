namespace Nethereum.Generators.Net.ContractLibrary
{
    public class DefaultNamespaceService
    {
        public static string SetDefaultCqs(string contractName)
        {
            return contractName + "." + "CQS";
        }

        public static string SetDefaultDto(string contractName)
        {
            return contractName + "." + "DTOs";
        }

        public static string SetDefaultService(string contractName)
        {
            return contractName + "." + "Service";
        }
    }
}