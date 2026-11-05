using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.X402.IntegrationTests.Helpers;

public static class GasBurningContracts
{
    public const string TokenAddress = "0x00000000000000000000000000000000000A9910";

    public static readonly byte[] BurnsGasOnEveryCallExceptBalanceAndAuthorizationState = string.Concat(
        "6000", "35", "60e0", "1c",
        "80", "6370a08231", "14", "6027", "57",
        "80", "63e94a0102", "14", "6035", "57",
        "61ffff", "5b", "6001", "90", "03", "80", "601d", "57", "00",
        "5b", "6001", "60c8", "1b", "6000", "52", "6020", "6000", "f3",
        "5b", "6000", "6000", "52", "6020", "6000", "f3").HexToByteArray();
}
