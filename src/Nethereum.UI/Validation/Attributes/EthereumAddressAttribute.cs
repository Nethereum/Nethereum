using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if NETCOREAPP3_1_OR_GREATER
using System.ComponentModel.DataAnnotations;

namespace Nethereum.UI.Validation.Attributes
{
    public sealed class EthereumAddressAttribute : ValidationAttribute
    {
        public EthereumAddressAttribute()
        {

        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null) return new ValidationResult("Address cannot be null");
            if (value.ToString().IsValidEthereumAddressHexFormat()) return ValidationResult.Success;
            return new ValidationResult("Invalid Ethereum address");
        }
    }

}
#endif