using System.Numerics;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public sealed record DiagnosticsGasPanel(
        BigInteger RequestedCallGasLimit,
        BigInteger RequestedVerificationGasLimit,
        BigInteger RequestedPreVerificationGas,
        BigInteger ActualGasUsed,
        BigInteger ActualGasCost,
        BigInteger TransactionGasUsed,
        BigInteger EffectiveGasPrice,
        BigInteger? CallGasBuffer,
        BigInteger? VerificationGasBuffer,
        BigInteger? PreVerificationGasBuffer,
        decimal CallGasMultiplier,
        decimal VerificationGasMultiplier);

    public sealed record DiagnosticsBundlerLookup(
        string Sender,
        BigInteger Nonce,
        string CallDataHex,
        BigInteger CallGasLimit,
        BigInteger VerificationGasLimit,
        BigInteger PreVerificationGas,
        BigInteger MaxFeePerGas,
        BigInteger MaxPriorityFeePerGas,
        string Paymaster,
        string SignatureHex,
        string EntryPoint,
        bool IsPending,
        BigInteger? BlockNumber,
        string? TransactionHash);

    public sealed record DiagnosticsPackedView(
        string AccountGasLimitsHex,
        string GasFeesHex,
        BigInteger UnpackedVerificationGasLimit,
        BigInteger UnpackedCallGasLimit,
        BigInteger UnpackedMaxPriorityFeePerGas,
        BigInteger UnpackedMaxFeePerGas,
        BigInteger UnpackedPaymasterVerificationGasLimit,
        BigInteger UnpackedPaymasterPostOpGasLimit,
        BigInteger TotalGas);
}
