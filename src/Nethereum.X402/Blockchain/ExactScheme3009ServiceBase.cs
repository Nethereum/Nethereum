using Nethereum.Documentation;
using System;
using System.Collections.Generic;
using System.Numerics;
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
using Nethereum.Web3.Accounts;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using Nethereum.X402.Signers;

namespace Nethereum.X402.Blockchain;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "ExactScheme3009ServiceBase - shared EIP-3009 verify and settle")]
public abstract class ExactScheme3009ServiceBase : IX402PaymentProcessor
{
    protected readonly IAccount Account;
    protected readonly Dictionary<int, IClient> ClientsByChainId;
    protected readonly TransferWithAuthorisationSigner Signer = new();
    protected readonly ILogger Logger;

    private readonly PaymentAuthorizationValidator _validator = new();
    private readonly X402FacilitatorOptions _options;
    private readonly FacilitatorAccounts _accounts;

    internal FacilitatorTransactionSender Sender { get; }

    protected ExactScheme3009ServiceBase(IAccount account, Dictionary<int, IClient> clientsByChainId, ILogger logger = null)
        : this(account, clientsByChainId, (X402FacilitatorOptions)null, logger)
    {
    }

    protected ExactScheme3009ServiceBase(
        IAccount account,
        Dictionary<int, IClient> clientsByChainId,
        X402FacilitatorOptions options,
        ILogger logger = null)
    {
        Account = account ?? throw new ArgumentNullException(nameof(account));
        _accounts = new FacilitatorAccounts(account);
        ClientsByChainId = clientsByChainId ?? throw new ArgumentNullException(nameof(clientsByChainId));
        Logger = logger ?? NullLogger.Instance;

        options?.Validate();
        _options = options ?? new X402FacilitatorOptions();
        Sender = new FacilitatorTransactionSender(_options);
    }

    protected abstract bool IsRecipientValid(Authorization authorization, PaymentRequirements requirements);

    protected abstract string RecoverSigner(Authorization authorization, ResolvedRequirement resolved, EthECDSASignature signature);

    protected abstract byte[] ComputeSignedDigest(Authorization authorization, ResolvedRequirement resolved);

    protected abstract Task<TransactionReceipt> SubmitSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature, CancellationTokenSource cancellationTokenSource);

    protected virtual Task<TransactionReceipt> SubmitContractSignatureSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, byte[] signature, CancellationTokenSource cancellationTokenSource) =>
        throw new NotSupportedException("This service does not settle with a signature that is not 65 bytes.");

    protected virtual Task<BigInteger> EstimateContractSignatureSettlementGasAsync(
        Eip3009Service eip3009Service, Authorization authorization, byte[] signature) =>
        throw new NotSupportedException("This service does not settle with a signature that is not 65 bytes.");

    protected abstract Task SimulateSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature);

    protected virtual async Task<BigInteger> EstimateSettlementGasAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature)
    {
        await SimulateSettlementAsync(eip3009Service, authorization, signature);
        return BigInteger.Zero;
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default) =>
        (await VerifyCoreAsync(paymentPayload, requirements, cancellationToken)).Verification;

    private async Task<VerifyOutcome> VerifyCoreAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken)
    {
        try
        {
            var protocolRejection = ExactVerification.ProtocolRejection(paymentPayload, requirements, X402AssetTransferMethods.Eip3009);
            if (protocolRejection != null)
                return Rejected(protocolRejection, null);

            if (!string.Equals(paymentPayload.Accepted?.Network, requirements.Network, StringComparison.OrdinalIgnoreCase))
                return Rejected(X402ErrorCodes.NetworkMismatch, null);

            var assetRejection = ExactVerification.AssetRejection(_options, requirements);
            if (assetRejection != null)
                return Rejected(assetRejection, null);

            var exactPayload = GetExactSchemePayload(paymentPayload);
            if (exactPayload?.Authorization == null)
                return Rejected(X402ErrorCodes.InvalidPayload, null);

            var authorization = exactPayload.Authorization;

            if (HasMissingFields(exactPayload, requirements) || !IsCanonicalNonce(authorization.Nonce))
                return Rejected(X402ErrorCodes.InvalidPayload, null);

            cancellationToken.ThrowIfCancellationRequested();

            if (!IsRecipientValid(authorization, requirements))
                return Rejected(X402ErrorCodes.RecipientMismatch, authorization.From);

            if (X402Number.Parse(authorization.Value) != X402Number.Parse(requirements.Amount))
                return Rejected(X402ErrorCodes.InvalidValue, authorization.From, "The authorized value does not equal the required amount.");

            if (!TryResolveRequirement(requirements, out var resolved, out var requirementError))
                return Rejected(requirementError, authorization.From);

            var signature = PayloadSignature.From(exactPayload.Signature);
            var signerWeb3 = new Nethereum.Web3.Web3(resolved.Client);
            if (!await PayerSignatureCheck.IsValidAsync(
                    signerWeb3, authorization.From, signature.Bytes,
                    () => TryRecoverSigner(authorization, resolved, signature.Ecdsa),
                    () => ComputeSignedDigest(authorization, resolved)))
                return await RejectedSignatureAsync(signerWeb3, authorization.From, signature.Bytes.Length);

            cancellationToken.ThrowIfCancellationRequested();
            var funds = await _validator.ValidateFundsNonceTimeAsync(
                authorization, resolved.TokenAddress, resolved.Client, cancellationToken);

            if (!funds.IsValid)
                return Rejected(funds.InvalidReason, authorization.From);

            cancellationToken.ThrowIfCancellationRequested();
            var simulationFailure = await SimulationFailureAsync(authorization, resolved, signature);
            if (simulationFailure != null)
                return simulationFailure.Value;

            return new VerifyOutcome(
                new VerificationResponse { IsValid = true, InvalidReason = null, Payer = authorization.From },
                funds.BlockNumber);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var reason = ExactVerification.FailureReason(Logger, "exact/eip3009", ex);
            return Rejected(
                reason, null,
                reason == X402ErrorCodes.UnexpectedVerifyError ? "The facilitator could not verify the payment." : null);
        }
    }

    public async Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        var outcome = await VerifyCoreAsync(paymentPayload, requirements, cancellationToken);
        var verification = outcome.Verification;
        if (!verification.IsValid)
            return SettlementResponse.Failure(requirements.Network, verification.InvalidReason, verification.Payer, message: verification.InvalidMessage);

        try
        {
            var exactPayload = GetExactSchemePayload(paymentPayload);
            var authorization = exactPayload.Authorization;
            TryResolveRequirement(requirements, out var resolved, out _);

            var web3 = new Nethereum.Web3.Web3(AccountFor(resolved.ChainId), resolved.Client);
            var eip3009Service = new Eip3009Service(web3, resolved.TokenAddress);
            var signature = PayloadSignature.From(exactPayload.Signature);
            var amount = X402Number.Parse(requirements.Amount);

            using var receiptBound = new SettlementBound(Sender.Bound(requirements.MaxTimeoutSeconds));
            TransactionReceipt receipt;
            try
            {
                receipt = await SubmitAsync(eip3009Service, authorization, signature, receiptBound);
            }
            catch (Exception ex) when (ex is not X402SettlementPendingException
                                       and not X402SettlementGasExceededException
                                       and not OperationCanceledException)
            {
                var settledByOther = await FindSettledByOtherAsync(web3, resolved, authorization, requirements, amount, outcome.VerifyBlock);
                if (settledByOther == null)
                    throw;

                receipt = await settledByOther.ConfirmAsync(Sender, web3, receiptBound);
                Logger.LogWarning(ex, "x402 exact/eip3009 settlement for payer {Payer} reverted; the payment was already settled by {Transaction}", verification.Payer, receipt.TransactionHash);
            }

            if (receipt.Status.Value != 1)
            {
                var settledByOther = await FindSettledByOtherAsync(web3, resolved, authorization, requirements, amount, outcome.VerifyBlock);
                if (settledByOther == null)
                    return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.TransactionFailed, authorization.From, receipt.TransactionHash, "The settlement transaction was mined and reverted.");

                receipt = await settledByOther.ConfirmAsync(Sender, web3, receiptBound);
            }

            if (!SettlementTransferCheck.HasExactlyOneMatchingTransfer(
                    receipt, requirements.Asset, authorization.From, requirements.PayTo, amount))
                return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.TransferEventMismatch, authorization.From, receipt.TransactionHash);

            return SettlementResponse.Succeeded(requirements.Network, authorization.From, receipt.TransactionHash, authorization.Value);
        }
        catch (X402SettlementGasExceededException ex)
        {
            Logger.LogWarning(ex, "x402 exact/eip3009 settlement for payer {Payer} refused: gas estimate above the ceiling", verification.Payer);
            return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.SettlementGasExceedsLimit, verification.Payer);
        }
        catch (X402SettlementPendingException ex)
        {
            Logger.LogWarning(ex, "x402 exact/eip3009 settlement {Transaction} broadcast but receipt not obtained for payer {Payer}", ex.TransactionHash, verification.Payer);
            return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.SettlementPending, verification.Payer, ex.TransactionHash);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "x402 exact/eip3009 settle failed for payer {Payer}", verification.Payer);
            return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.UnexpectedSettleError, verification.Payer);
        }
    }

    private async Task<RecoveredSettlement> FindSettledByOtherAsync(
        Nethereum.Web3.Web3 web3,
        ResolvedRequirement resolved,
        Authorization authorization,
        PaymentRequirements requirements,
        BigInteger amount,
        BigInteger verifyBlock)
    {
        try
        {
            return await FrontRunSettlementRecovery.FindEip3009SettlementAsync(
                web3, resolved.TokenAddress, authorization.From, authorization.Nonce, requirements.PayTo, amount, verifyBlock,
                new RecoveryScope(resolved.ChainId, AccountFor(resolved.ChainId).Address, X402Number.Parse(authorization.ValidBefore), _options.FacilitatorAddresses));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogDebug(ex, "x402 exact/eip3009 front-run settlement lookup failed for payer {Payer}", authorization.From);
            return null;
        }
    }

    private Task<TransactionReceipt> SubmitAsync(
        Eip3009Service eip3009Service, Authorization authorization, PayloadSignature signature, CancellationTokenSource cancellationTokenSource) =>
        signature.Ecdsa != null
            ? SubmitSettlementAsync(eip3009Service, authorization, signature.Ecdsa, cancellationTokenSource)
            : SubmitContractSignatureSettlementAsync(eip3009Service, authorization, signature.Bytes, cancellationTokenSource);

    private Task<BigInteger> EstimateAsync(
        Eip3009Service eip3009Service, Authorization authorization, PayloadSignature signature) =>
        signature.Ecdsa != null
            ? EstimateSettlementGasAsync(eip3009Service, authorization, signature.Ecdsa)
            : EstimateContractSignatureSettlementGasAsync(eip3009Service, authorization, signature.Bytes);

    private async Task<VerifyOutcome?> SimulationFailureAsync(
        Authorization authorization, ResolvedRequirement resolved, PayloadSignature signature)
    {
        try
        {
            var web3 = new Nethereum.Web3.Web3(AccountFor(resolved.ChainId), resolved.Client);
            var eip3009Service = new Eip3009Service(web3, resolved.TokenAddress);
            var estimate = await EstimateAsync(eip3009Service, authorization, signature);
            return estimate > _options.MaxSettlementGas
                ? Rejected(X402ErrorCodes.SettlementGasExceedsLimit, authorization.From, "The settlement gas estimate is above the facilitator ceiling.")
                : null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "x402 exact/eip3009 verify simulation reverted for payer {Payer}", authorization.From);
            return await DiagnosedAsync(
                new Nethereum.Web3.Web3(resolved.Client), resolved, authorization.From,
                Rejected(X402ErrorCodes.TransactionSimulationFailed, authorization.From, "The settlement transaction reverted when simulated."));
        }
    }

    private static async Task<VerifyOutcome> RejectedSignatureAsync(Nethereum.Web3.Web3 web3, string payer, int signatureLength)
    {
        if (signatureLength > PayerSignatureCheck.EcdsaSignatureLength)
        {
            var code = await web3.Eth.GetCode.SendRequestAsync(payer);
            if (string.IsNullOrEmpty(code) || code.RemoveHexPrefix().Length == 0)
                return Rejected(X402ErrorCodes.UndeployedSmartWallet, payer, "The signature is longer than 65 bytes and the payer has no deployed code.");
        }

        return Rejected(X402ErrorCodes.InvalidSignature, payer, "The signature does not recover to the payer and is not a valid ERC-1271 signature.");
    }

    private static async Task<VerifyOutcome> DiagnosedAsync(
        Nethereum.Web3.Web3 web3, ResolvedRequirement resolved, string payer, VerifyOutcome fallback)
    {
        var onchainName = await Eip3009TokenDomainReader.NameAsync(web3, resolved.TokenAddress);
        if (onchainName != null && onchainName != resolved.TokenName)
            return Rejected(
                X402ErrorCodes.TokenNameMismatch, payer,
                $"The token reports the name '{onchainName}' but the requirements declare '{resolved.TokenName}'.");

        var onchainVersion = await Eip3009TokenDomainReader.VersionAsync(web3, resolved.TokenAddress);
        if (onchainVersion != null && onchainVersion != resolved.TokenVersion)
            return Rejected(
                X402ErrorCodes.TokenVersionMismatch, payer,
                $"The token reports the version '{onchainVersion}' but the requirements declare '{resolved.TokenVersion}'.");

        return fallback;
    }

    private string? TryRecoverSigner(Authorization authorization, ResolvedRequirement resolved, EthECDSASignature? signature)
    {
        if (signature == null)
            return null;

        try
        {
            return RecoverSigner(authorization, resolved, signature);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogDebug(ex, "x402 exact/eip3009 verify: the signature does not recover");
            return null;
        }
    }

    protected IAccount AccountFor(int chainId) => _accounts.For(chainId);

    private static bool IsCanonicalNonce(string nonce) =>
        X402Address.HasPrefix(nonce) && X402Address.TryCanonicalWord(nonce, out _);

    private static bool HasMissingFields(ExactSchemePayload payload, PaymentRequirements requirements)
    {
        var authorization = payload.Authorization;
        return string.IsNullOrEmpty(payload.Signature) ||
               string.IsNullOrEmpty(authorization.From) ||
               string.IsNullOrEmpty(authorization.To) ||
               string.IsNullOrEmpty(authorization.Value) ||
               string.IsNullOrEmpty(authorization.ValidAfter) ||
               string.IsNullOrEmpty(authorization.ValidBefore) ||
               string.IsNullOrEmpty(authorization.Nonce) ||
               string.IsNullOrEmpty(requirements.Amount) ||
               string.IsNullOrEmpty(requirements.PayTo);
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
        return Task.FromResult(new SupportedPaymentKindsResponse
        {
            Kinds = kinds,
            Signers = SupportedSigners.ForAccount(Account.Address)
        });
    }

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
            error = X402ErrorCodes.MissingEip712Domain;
            return false;
        }

        resolved = new ResolvedRequirement(chainId, client, requirements.Asset, extra.Name, extra.Version);
        error = null;
        return true;
    }

    protected static ExactSchemePayload GetExactSchemePayload(PaymentPayload paymentPayload) =>
        ExactVerification.ReadPayload<ExactSchemePayload>(paymentPayload);

    private static VerifyOutcome Rejected(string reason, string payer, string? message = null) =>
        new(new VerificationResponse { IsValid = false, InvalidReason = reason, InvalidMessage = message, Payer = payer }, BigInteger.Zero);

    private sealed record PayloadSignature(byte[] Bytes, EthECDSASignature? Ecdsa)
    {
        public static PayloadSignature From(string signatureHex)
        {
            var bytes = signatureHex.HexToByteArray();
            return new PayloadSignature(
                bytes,
                bytes.Length == PayerSignatureCheck.EcdsaSignatureLength ? EthECDSASignatureFactory.ExtractECDSASignature(signatureHex) : null);
        }
    }

    private readonly record struct VerifyOutcome(VerificationResponse Verification, BigInteger VerifyBlock);

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
