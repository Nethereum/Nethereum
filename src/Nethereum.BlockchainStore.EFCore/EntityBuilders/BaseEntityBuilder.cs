namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{

    public abstract class BaseEntityBuilder
    {
        public string ColumnTypeForUnlimitedText = "nvarchar(max)";
    }
}