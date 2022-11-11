using Nethereum.Hex.HexConvertors.Extensions;
#if NETCOREAPP3_1_OR_GREATER
using System.ComponentModel.DataAnnotations;

namespace Nethereum.UI.Validation.Attributes
{
    public sealed class HexAttribute : ValidationAttribute
    {
        public HexAttribute()
        {

        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null) return new ValidationResult("Hex value cannot be null");
            if (value.ToString().IsHex()) return ValidationResult.Success;
            return new ValidationResult("Invalid hex value");
        }
    }

}
#endif