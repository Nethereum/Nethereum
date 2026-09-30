using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Contracts.EIP3009.EIP3009;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using Nethereum.X402.Signers;

namespace Nethereum.X402.Blockchain;

public abstract class ExactScheme3009ServiceBase : IX402PaymentProcessor
{
    protected readonly IAccount Account;
    protected readonly Dictionary<int, IClient> ClientsByChainId;
    protected readonly TransferWithAuthorisationSigner Signer = new();
    protected readonly ILogger Logger;
    private readonly PaymentAuthorizationValidator _validator = new();

    protected ExactScheme3009ServiceBase(IAccount account, Dictionary<int, IClient> clientsByChainId, ILogger logger = null)
    {
        Account = account ?? throw new ArgumentNullException(nameof(account));
        ClientsByChainId = clientsByChainId ?? throw new ArgumentNullException(nameof(clientsByChainId));
        Logger = logger ?? NullLogger.Instance;
    }

    protected abstract bool IsRecipientValid(Authorization authorization, PaymentRequirements requirements);

    /// <summary>Recovers the signer address using this variant's EIP-712 domain (Transfer vs Receive).</summary>
    protected abstract string RecoverSigner(Authorization authorization, ResolvedRequirement resolved, EthECDSASignature signature);

    /// <summary>Computes the EIP-712 digest a payer signs, for the ERC-1271 contract-wallet fallback.</summary>
    protected abstract byte[] ComputeSignedDigest(Authorization authorization, ResolvedRequirement resolved);

    /// <summary>Submits this variant's EIP-3009 call and returns the receipt.</summary>
    protected abstract Task<TransactionReceipt> SubmitSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature, CancellationTokenSource cancellationTokenSource);

    /// <summary>
    /// Dry-runs this variant's EIP-3009 call (eth_estimateGas from the settling account) so a
    /// settlement that would revert on-chain — a paused or blocklisting token, a fee-on-transfer
    /// balance shortfall — is caught before the facilitator spends gas. Throws if the call reverts.
    /// </summary>
    protected abstract Task SimulateSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature);

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

            if (!string.Equals(paymentPayload.Accepted?.Network, requirements.Network, StringComparison.OrdinalIgnoreCase))
                return Invalid(X402ErrorCodes.NetworkMismatch, null);

            var exactPayload = GetExactSchemePayload(paymentPayload);
            if (exactPayload == null)
                return Invalid(X402ErrorCodes.InvalidPayload, null);

            var authorization = exactPayload.Authorization;

            if (!IsRecipientValid(authorization, requirements))
                return Invalid(X402ErrorCodes.RecipientMismatch, authorization.From);

            if (BigInteger.Parse(authorization.Value) != BigInteger.Parse(requirements.Amount))
                return Invalid(X402ErrorCodes.InvalidValue, authorization.From);

            if (!TryResolveRequirement(requirements, out var resolved, out var requirementError))
                return Invalid(requirementError, authorization.From);

            var signature = EthECDSASignatureFactory.ExtractECDSASignature(exactPayload.Signature);
            var recovered = RecoverSigner(authorization, resolved, signature);
            if (!authorization.From.IsTheSameAddress(recovered))
            {
                // Plain ECDSA recovery did not match — the payer may be an ERC-1271 contract wallet.
                var web3 = new Nethereum.Web3.Web3(resolved.Client);
                var digest = ComputeSignedDigest(authorization, resolved);
                var contractSignatureValid = await Erc1271SignatureVerifier.IsValidContractSignatureAsync(
                    web3, authorization.From, digest, exactPayload.Signature.HexToByteArray());
                if (!contractSignatureValid)
                    return Invalid(X402ErrorCodes.InvalidSignature, authorization.From);
            }

            var funds = await _validator.ValidateFundsNonceTimeAsync(
                authorization, resolved.TokenAddress, resolved.Client, cancellationToken);

            return new VerificationResponse
            {
                IsValid = funds.IsValid,
                InvalidReason = funds.InvalidReason,
                Payer = authorization.From
            };
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "x402 exact/eip3009 verify: malformed payment payload");
            return Invalid(X402ErrorCodes.InvalidPayload, null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "x402 exact/eip3009 verify failed");
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
            var exactPayload = GetExactSchemePayload(paymentPayload);
            var authorization = exactPayload.Authorization;
            TryResolveRequirement(requirements, out var resolved, out _);

            var web3 = new Nethereum.Web3.Web3(Account, resolved.Client);
            var eip3009Service = new Eip3009Service(web3, resolved.TokenAddress);
            var signature = EthECDSASignatureFactory.ExtractECDSASignature(exactPayload.Signature);

            try
            {
                await SimulateSettlementAsync(eip3009Service, authorization, signature);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "x402 exact/eip3009 settle simulation reverted for payer {Payer}", authorization.From);
                return new SettlementResponse
                {
                    Success = false,
                    ErrorReason = X402ErrorCodes.TransactionSimulationFailed,
                    Transaction = null,
                    Network = requirements.Network,
                    Payer = authorization.From
                };
            }

            var cancellationTokenSource = cancellationToken != default
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            var receipt = await SubmitSettlementAsync(eip3009Service, authorization, signature, cancellationTokenSource);

            if (receipt.Status.Value == 1)
                return new SettlementResponse
                {
                    Success = true,
                    ErrorReason = null,
                    Transaction = receipt.TransactionHash,
                    Network = requirements.Network,
                    Payer = authorization.From,
                    Amount = authorization.Value
                };

            return new SettlementResponse
            {
                Success = false,
                ErrorReason = X402ErrorCodes.InvalidTransactionState,
                Transaction = receipt.TransactionHash,
                Network = requirements.Network,
                Payer = authorization.From
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "x402 exact/eip3009 settle failed for payer {Payer}", verification.Payer);
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
        foreach (var chainId in ClientsByChainId.Keys)
        {
            kinds.Add(new PaymentKind
            {
                X402Version = X402Protocol.Version,
                Scheme = X402Schemes.Exact,
                Network = Caip2.FormatEip155(chainId),
                Extra = null
            });
        }
        return Task.FromResult(new SupportedPaymentKindsResponse { Kinds = kinds });
    }

    /// <summary>
    /// Resolves the settlement inputs for a requirement: chain ID from the CAIP-2 network, token
    /// address from the asset, EIP-712 domain from the extra data, and RPC endpoint from the
    /// configured per-chain map.
    /// </summary>
    protected bool TryResolveRequirement(PaymentRequirements requirements, out ResolvedRequirement resolved, out string error)
    {
        resolved = default;

        if (!Caip2.TryParseEip155ChainId(requirements.Network, out var chainId))
        {
            error = X402ErrorCodes.InvalidNetwork;
            return false;
        }

        if (!ClientsByChainId.TryGetValue(chainId, out var client))
        {
            error = X402ErrorCodes.InvalidNetwork;
            return false;
        }

        if (string.IsNullOrEmpty(requirements.Asset))
        {
            error = X402ErrorCodes.InvalidPaymentRequirements;
            return false;
        }

        var extra = ExactSchemeExtra.FromRequirements(requirements);
        if (extra == null || string.IsNullOrEmpty(extra.Name) || string.IsNullOrEmpty(extra.Version))
        {
            error = X402ErrorCodes.InvalidPaymentRequirements;
            return false;
        }

        resolved = new ResolvedRequirement(chainId, client, requirements.Asset, extra.Name, extra.Version);
        error = null;
        return true;
    }

    protected static ExactSchemePayload GetExactSchemePayload(PaymentPayload paymentPayload)
    {
        if (paymentPayload.Payload is ExactSchemePayload exactPayload)
            return exactPayload;

        if (paymentPayload.Payload is JsonElement jsonElement)
            return JsonSerializer.Deserialize<ExactSchemePayload>(jsonElement.GetRawText());

        return null;
    }

    private static VerificationResponse Invalid(string reason, string payer) =>
        new() { IsValid = false, InvalidReason = reason, Payer = payer };

    protected readonly struct ResolvedRequirement
    {
        public ResolvedRequirement(int chainId, IClient client, string tokenAddress, string tokenName, string tokenVersion)
        {
            ChainId = chainId;
            Client = client;
            TokenAddress = tokenAddress;
            TokenName = tokenName;
            TokenVersion = tokenVersion;
        }

        public int ChainId { get; }
        public IClient Client { get; }
        public string TokenAddress { get; }
        public string TokenName { get; }
        public string TokenVersion { get; }
    }
}
