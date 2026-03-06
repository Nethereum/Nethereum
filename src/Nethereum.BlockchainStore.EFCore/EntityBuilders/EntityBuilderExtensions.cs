using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public static class EntityBuilderExtensions
    {
        public static PropertyBuilder<TProperty> IsUnlimitedText<TProperty>(
            this PropertyBuilder<TProperty> propertyBuilder, string columnTypeForUnlimitedText)
        {
            return propertyBuilder.HasColumnType(columnTypeForUnlimitedText);
        }

        public static PropertyBuilder<TProperty> IsAddress<TProperty>(
            this PropertyBuilder<TProperty> propertyBuilder)
        {
            return propertyBuilder.HasMaxLength(ColumnLengths.AddressLength);
        }

        public static PropertyBuilder<TProperty> IsHash<TProperty>(
            this PropertyBuilder<TProperty> propertyBuilder)
        {
            return propertyBuilder.HasMaxLength(ColumnLengths.HashLength);
        }

        public static PropertyBuilder<TProperty> IsBigInteger<TProperty>(
            this PropertyBuilder<TProperty> propertyBuilder)
        {
            return propertyBuilder.HasMaxLength(ColumnLengths.BigIntegerLength);
        }
    }
}