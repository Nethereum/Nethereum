using Nethereum.Documentation;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.X402.Models;

namespace Nethereum.X402.Permit2;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2SettlementErrorMapper - map a settlement revert to a reason")]
public static class Permit2SettlementErrorMapper
{
    public static string MapReason(SmartContractCustomErrorRevertException exception)
    {
        if (exception.IsCustomErrorFor<Permit2612AmountMismatchError>())
            return X402ErrorCodes.Permit2612AmountMismatch;

        if (exception.IsCustomErrorFor<InvalidDestinationError>())
            return X402ErrorCodes.Permit2InvalidDestination;

        if (exception.IsCustomErrorFor<InvalidOwnerError>())
            return X402ErrorCodes.Permit2InvalidOwner;

        if (exception.IsCustomErrorFor<PaymentTooEarlyError>())
            return X402ErrorCodes.Permit2PaymentTooEarly;

        var name = Permit2Service.FindCustomError(exception)?.ErrorABI?.Name;
        if (name == null) return null;

        return name switch
        {
            "InvalidNonce" => X402ErrorCodes.Permit2InvalidNonce,
            "SignatureExpired" => X402ErrorCodes.Permit2DeadlineExpired,
            "AllowanceExpired" => X402ErrorCodes.Permit2AllowanceRequired,
            "InsufficientAllowance" => X402ErrorCodes.Permit2AllowanceRequired,
            "InvalidAmount" => X402ErrorCodes.Permit2InvalidAmount,
            "InvalidSigner" => X402ErrorCodes.Permit2InvalidSignature,
            "InvalidContractSignature" => X402ErrorCodes.Permit2InvalidSignature,
            "InvalidSignature" => X402ErrorCodes.Permit2InvalidSignature,
            "InvalidSignatureLength" => X402ErrorCodes.Permit2InvalidSignature,
            _ => null
        };
    }
}
