using FluentValidation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;

namespace Nethereum.UI.Validation
{
    public static class EthereumRules
    {
        public static IRuleBuilderInitial<T, string> IsUri<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.Custom((value, context) => {
                if (!Uri.IsWellFormedUriString(value, UriKind.RelativeOrAbsolute))
                    context.AddFailure("Invalid Uri");
            });
        }

        public static IRuleBuilderInitial<T, string> IsEthereumAddress<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.Custom((value, context) => {
                if (!value.IsValidEthereumAddressHexFormat())
                    context.AddFailure("Invalid Ethereum Address");
            });
        }
        public static IRuleBuilderInitial<T, string> IsHex<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.NotEmpty().Custom((value, context) => {
                if (!value.HasHexPrefix()) context.AddFailure("The value needs to be prefixed with 0x");
                if (!value.IsHex()) context.AddFailure("This is not a valid Hexadecimal");
            });
        }

        public static IRuleBuilderOptions<T, string> IsEthereumPrivateKey<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            //0x + 64
            return ruleBuilder.IsHex().MaximumLength(66).MinimumLength(66);
        }
    }

}
