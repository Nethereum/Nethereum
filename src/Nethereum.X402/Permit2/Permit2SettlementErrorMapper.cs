using Nethereum.Contracts;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.X402.Models;

namespace Nethereum.X402.Permit2;

public static class Permit2SettlementErrorMapper
{
    public static string MapReason(SmartContractCustomErrorRevertException exception)
    {
        var name = Permit2Service.FindCustomError(exception)?.ErrorABI?.Name;
        if (name == null) return null;

        return name switch
        {
            "InvalidNonce" => X402ErrorCodes.NonceAlreadyUsed,
            "SignatureExpired" => X402ErrorCodes.Permit2DeadlineExpired,
            "AllowanceExpired" => X402ErrorCodes.Permit2AllowanceRequired,
            "InsufficientAllowance" => X402ErrorCodes.Permit2AllowanceRequired,
            "InvalidAmount" => X402ErrorCodes.Permit2AmountMismatch,
            "InvalidSigner" => X402ErrorCodes.Permit2InvalidSignature,
            "InvalidContractSignature" => X402ErrorCodes.Permit2InvalidSignature,
            "InvalidSignature" => X402ErrorCodes.Permit2InvalidSignature,
            "InvalidSignatureLength" => X402ErrorCodes.Permit2InvalidSignature,
            _ => null
        };
    }
}
