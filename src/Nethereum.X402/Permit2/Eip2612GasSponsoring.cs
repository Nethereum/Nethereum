using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.X402.Models;

namespace Nethereum.X402.Permit2;

internal static class Eip2612GasSponsoring
{
    public const int DeadlineBufferSeconds = 6;


    public static Eip2612GasSponsoringInfo? Extract(object? extensions)
    {
        if (!X402ExtensionInfoReader.TryGetInfo(extensions, X402ExtensionKeys.Eip2612GasSponsoring, out var info))
            return null;

        var parsed = new Eip2612GasSponsoringInfo
        {
            From = X402ExtensionInfoReader.ReadString(info, "from")!,
            Asset = X402ExtensionInfoReader.ReadString(info, "asset")!,
            Spender = X402ExtensionInfoReader.ReadString(info, "spender")!,
            Amount = X402ExtensionInfoReader.ReadString(info, "amount")!,
            Nonce = X402ExtensionInfoReader.ReadString(info, "nonce")!,
            Deadline = X402ExtensionInfoReader.ReadString(info, "deadline")!,
            Signature = X402ExtensionInfoReader.ReadString(info, "signature")!,
            Version = X402ExtensionInfoReader.ReadString(info, "version")!
        };

        return string.IsNullOrEmpty(parsed.From) || string.IsNullOrEmpty(parsed.Asset) ||
               string.IsNullOrEmpty(parsed.Spender) || string.IsNullOrEmpty(parsed.Amount) ||
               string.IsNullOrEmpty(parsed.Nonce) || string.IsNullOrEmpty(parsed.Deadline) ||
               string.IsNullOrEmpty(parsed.Signature) || string.IsNullOrEmpty(parsed.Version)
            ? null
            : parsed;
    }

    public static bool HasValidFormat(Eip2612GasSponsoringInfo info) =>
        X402GasSponsoringFormat.IsAddress(info.From) &&
        X402GasSponsoringFormat.IsAddress(info.Asset) &&
        X402GasSponsoringFormat.IsAddress(info.Spender) &&
        X402GasSponsoringFormat.IsNumber(info.Amount) &&
        X402GasSponsoringFormat.IsNumber(info.Nonce) &&
        X402GasSponsoringFormat.IsNumber(info.Deadline) &&
        X402GasSponsoringFormat.IsHex(info.Signature) &&
        X402GasSponsoringFormat.IsVersion(info.Version);

    public static string? Validate(Eip2612GasSponsoringInfo info, string payer, string token, long nowUnixSeconds)
    {
        if (!HasValidFormat(info))
            return X402ErrorCodes.InvalidEip2612ExtensionFormat;

        if (!info.From.IsTheSameAddress(payer))
            return X402ErrorCodes.Eip2612FromMismatch;

        if (!info.Asset.IsTheSameAddress(token))
            return X402ErrorCodes.Eip2612AssetMismatch;

        if (!info.Spender.IsTheSameAddress(X402Permit2Addresses.Permit2))
            return X402ErrorCodes.Eip2612SpenderNotPermit2;

        if (X402Number.Parse(info.Deadline) < nowUnixSeconds + DeadlineBufferSeconds)
            return X402ErrorCodes.Eip2612DeadlineExpired;

        return null;
    }

    public static Eip2612PermitParams ToPermitParams(Eip2612GasSponsoringInfo info)
    {
        var signature = info.Signature.HexToByteArray();
        if (signature.Length != 65)
            throw new ArgumentException(
                $"invalid EIP-2612 signature length: expected 65 bytes, got {signature.Length} bytes");

        return new Eip2612PermitParams
        {
            Value = X402Number.Parse(info.Amount),
            Deadline = X402Number.Parse(info.Deadline),
            R = signature[..32],
            S = signature[32..64],
            V = signature[64]
        };
    }
}
