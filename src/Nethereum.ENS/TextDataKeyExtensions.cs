namespace Nethereum.ENS
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.SmartContracts, "ens-contract-services", "TextDataKeyExtensions")]
    public static class TextDataKeyExtensions
    {
        public static string GetDataKeyAsString(this TextDataKey textDataKey)
        {
            return textDataKey.ToString().Replace("_", ".");
        }
    }
}
