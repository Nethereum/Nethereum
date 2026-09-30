using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Processors;

namespace Nethereum.X402.Blockchain;

public class X402ExactPermit2Service : IX402PaymentProcessor
{
    private readonly IAccount _facilitatorAccount;
    private readonly Dictionary<int, IClient> _clientsByChainId;
    private readonly Permit2WitnessSigner _signer = new();
    private readonly ILogger _logger;

    public X402ExactPermit2Service(
        string facilitatorPrivateKey,
        Dictionary<int, IClient> clientsByChainId,
        ILogger logger = null)
        : this(new Account(facilitatorPrivateKey), clientsByChainId, logger)
    {
    }

    public X402ExactPermit2Service(
        IAccount facilitatorAccount,
        Dictionary<int, IClient> clientsByChainId,
        ILogger logger = null)
    {
        _facilitatorAccount = facilitatorAccount ?? throw new ArgumentNullException(nameof(facilitatorAccount));
        _clientsByChainId = clientsByChainId ?? throw new ArgumentNullException(nameof(clientsByChainId));
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (paymentPayload.X402Version != X402Protocol.Version)
                return Invalid(X402ErrorCodes.InvalidX402Version, null);

            if (paymentPayload.Accepted?.Scheme != X402Schemes.Exact || requirements.Scheme != X402Schemes.Exact)
                return Invalid(X402ErrorCodes.UnsupportedScheme, null);

            var payload = GetPermit2Payload(paymentPayload);
            if (payload?.Permit2Authorization == null)
                return Invalid(X402ErrorCodes.InvalidPayload, null);

            var auth = payload.Permit2Authorization;
            var payer = auth.From;

            if (!string.Equals(paymentPayload.Accepted?.Network, requirements.Network, StringComparison.OrdinalIgnoreCase))
                return Invalid(X402ErrorCodes.NetworkMismatch, payer);

            if (!Caip2.TryParseEip155ChainId(requirements.Network, out var chainId) ||
                !_clientsByChainId.TryGetValue(chainId, out var client))
                return Invalid(X402ErrorCodes.InvalidNetwork, payer);

            if (!auth.Spender.IsTheSameAddress(X402Permit2Addresses.ExactPermit2Proxy))
                return Invalid(X402ErrorCodes.Permit2InvalidSpender, payer);

            if (!auth.Witness.To.IsTheSameAddress(requirements.PayTo))
                return Invalid(X402ErrorCodes.Permit2RecipientMismatch, payer);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (BigInteger.Parse(auth.Deadline) < now + 6)
                return Invalid(X402ErrorCodes.Permit2DeadlineExpired, payer);

            if (BigInteger.Parse(auth.Witness.ValidAfter) > now)
                return Invalid(X402ErrorCodes.Permit2NotYetValid, payer);

            if (BigInteger.Parse(auth.Permitted.Amount) != BigInteger.Parse(requirements.Amount))
                return Invalid(X402ErrorCodes.Permit2AmountMismatch, payer);

            if (!auth.Permitted.Token.IsTheSameAddress(requirements.Asset))
                return Invalid(X402ErrorCodes.Permit2TokenMismatch, payer);

            var web3 = new Web3.Web3(_facilitatorAccount, client);

            // Verify the EIP-712 signature recovers to the payer, falling back to ERC-1271 for a
            // contract wallet. Permit2 itself performs the same ecrecover/1271 check on settle, so a
            // contract-signature accepted here also settles on-chain.
            var witnessMessage = auth.ToWitnessMessage();
            var recovered = _signer.RecoverSigner(witnessMessage, chainId, X402Permit2Addresses.Permit2, payload.Signature);
            if (!recovered.IsTheSameAddress(payer))
            {
                var digest = _signer.ComputeDigest(witnessMessage, chainId, X402Permit2Addresses.Permit2);
                var contractSignatureValid = await Erc1271SignatureVerifier.IsValidContractSignatureAsync(
                    web3, payer, digest, payload.Signature.HexToByteArray());
                if (!contractSignatureValid)
                    return Invalid(X402ErrorCodes.Permit2InvalidSignature, payer);
            }

            var proxyCode = await web3.Eth.GetCode.SendRequestAsync(X402Permit2Addresses.ExactPermit2Proxy);
            if (string.IsNullOrEmpty(proxyCode) || proxyCode == "0x")
                return Invalid(X402ErrorCodes.Permit2ProxyNotDeployed, payer);

            var amount = BigInteger.Parse(auth.Permitted.Amount);
            var erc20 = web3.Eth.ERC20.GetContractService(auth.Permitted.Token);

            var balance = await erc20.BalanceOfQueryAsync(payer);
            if (balance < amount)
                return Invalid(X402ErrorCodes.Permit2InsufficientBalance, payer);

            var permit2Allowance = await erc20.AllowanceQueryAsync(payer, X402Permit2Addresses.Permit2);
            if (permit2Allowance < amount)
                return Invalid(X402ErrorCodes.Permit2AllowanceRequired, payer);

            var nonce = BigInteger.Parse(auth.Nonce);
            var nonceBitmap = await web3.Eth.GetPermit2Service().NonceBitmapQueryAsync(payer, nonce >> 8);
            if (((nonceBitmap >> (int)(nonce & 0xff)) & BigInteger.One) == BigInteger.One)
                return Invalid(X402ErrorCodes.NonceAlreadyUsed, payer);

            return new VerificationResponse { IsValid = true, InvalidReason = null, Payer = payer };
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "x402 exact/permit2 verify: malformed payment payload");
            return Invalid(X402ErrorCodes.InvalidPayload, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "x402 exact/permit2 verify failed");
            return Invalid(X402ErrorCodes.UnexpectedVerifyError, null);
        }
    }

    public async Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        var verification = await VerifyPaymentAsync(paymentPayload, requirements, cancellationToken);
        if (!verification.IsValid)
            return new SettlementResponse
            {
                Success = false,
                ErrorReason = verification.InvalidReason,
                Transaction = null,
                Network = requirements.Network,
                Payer = verification.Payer
            };

        try
        {
            var payload = GetPermit2Payload(paymentPayload);
            var auth = payload.Permit2Authorization;
            Caip2.TryParseEip155ChainId(requirements.Network, out var chainId);
            var client = _clientsByChainId[chainId];
            var web3 = new Web3.Web3(_facilitatorAccount, client);

            var settle = auth.ToSettleFunction(payload.Signature);
            var proxyHandler = web3.Eth.GetContractHandler(X402Permit2Addresses.ExactPermit2Proxy);

            try
            {
                await proxyHandler.EstimateGasAsync(settle);
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                var reason = Permit2SettlementErrorMapper.MapReason(ex) ?? X402ErrorCodes.TransactionSimulationFailed;
                _logger.LogWarning(ex, "x402 exact/permit2 settle simulation reverted ({Reason}) for payer {Payer}", reason, auth.From);
                return new SettlementResponse
                {
                    Success = false,
                    ErrorReason = reason,
                    Transaction = null,
                    Network = requirements.Network,
                    Payer = auth.From
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "x402 exact/permit2 settle simulation reverted for payer {Payer}", auth.From);
                return new SettlementResponse
                {
                    Success = false,
                    ErrorReason = X402ErrorCodes.TransactionSimulationFailed,
                    Transaction = null,
                    Network = requirements.Network,
                    Payer = auth.From
                };
            }

            var cts = cancellationToken != default
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            var receipt = await proxyHandler.SendRequestAndWaitForReceiptAsync(settle, cts);

            if (receipt.Status.Value == 1)
                return new SettlementResponse
                {
                    Success = true,
                    ErrorReason = null,
                    Transaction = receipt.TransactionHash,
                    Network = requirements.Network,
                    Payer = auth.From,
                    Amount = auth.Permitted.Amount
                };

            var revertReason = X402ErrorCodes.InvalidTransactionState;
            try
            {
                await web3.Eth.GetContractTransactionErrorReason.SendRequestAsync(receipt.TransactionHash);
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                revertReason = Permit2SettlementErrorMapper.MapReason(ex) ?? X402ErrorCodes.InvalidTransactionState;
                _logger.LogWarning(ex, "x402 exact/permit2 settle reverted on-chain ({Reason}) for payer {Payer}", revertReason, auth.From);
            }
            catch
            {
            }

            return new SettlementResponse
            {
                Success = false,
                ErrorReason = revertReason,
                Transaction = receipt.TransactionHash,
                Network = requirements.Network,
                Payer = auth.From
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "x402 exact/permit2 settle failed for payer {Payer}", verification.Payer);
            return new SettlementResponse
            {
                Success = false,
                ErrorReason = X402ErrorCodes.UnexpectedSettleError,
                Transaction = null,
                Network = requirements.Network,
                Payer = verification.Payer
            };
        }
    }

    public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default)
    {
        var kinds = new List<PaymentKind>();
        foreach (var chainId in _clientsByChainId.Keys)
        {
            kinds.Add(new PaymentKind
            {
                X402Version = X402Protocol.Version,
                Scheme = X402Schemes.Exact,
                Network = Caip2.FormatEip155(chainId),
                Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2 }
            });
        }
        return Task.FromResult(new SupportedPaymentKindsResponse { Kinds = kinds });
    }

    private static VerificationResponse Invalid(string reason, string payer) =>
        new() { IsValid = false, InvalidReason = reason, Payer = payer };

    private static Permit2SchemePayload GetPermit2Payload(PaymentPayload paymentPayload)
    {
        switch (paymentPayload.Payload)
        {
            case Permit2SchemePayload typed:
                return typed;
            case JsonElement jsonElement:
                return JsonSerializer.Deserialize<Permit2SchemePayload>(jsonElement.GetRawText());
            default:
                return null;
        }
    }
}
