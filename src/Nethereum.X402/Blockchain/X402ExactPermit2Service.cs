using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Facilitator;
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
    private readonly Erc20ApprovalGasSponsoringOptions _approvalSponsoring;
    private readonly IErc20ApprovalSponsoredSettlement _approvalSettlement;

    private readonly X402FacilitatorOptions _options;
    private readonly FacilitatorTransactionSender _sender;
    private readonly ConcurrentDictionary<string, byte> _approvalsInFlight = new();
    private readonly FacilitatorAccounts _accounts;

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
        : this(facilitatorAccount, clientsByChainId, null, logger)
    {
    }

    public X402ExactPermit2Service(
        string facilitatorPrivateKey,
        Dictionary<int, IClient> clientsByChainId,
        Erc20ApprovalGasSponsoringOptions erc20ApprovalGasSponsoring,
        ILogger logger = null)
        : this(new Account(facilitatorPrivateKey), clientsByChainId, erc20ApprovalGasSponsoring, logger)
    {
    }

    public X402ExactPermit2Service(
        string facilitatorPrivateKey,
        Dictionary<int, IClient> clientsByChainId,
        Erc20ApprovalGasSponsoringOptions erc20ApprovalGasSponsoring,
        X402FacilitatorOptions options,
        ILogger logger = null)
        : this(new Account(facilitatorPrivateKey), clientsByChainId, erc20ApprovalGasSponsoring, options, logger)
    {
    }

    public X402ExactPermit2Service(
        IAccount facilitatorAccount,
        Dictionary<int, IClient> clientsByChainId,
        Erc20ApprovalGasSponsoringOptions erc20ApprovalGasSponsoring,
        ILogger logger = null)
        : this(facilitatorAccount, clientsByChainId, erc20ApprovalGasSponsoring, (X402FacilitatorOptions)null, logger)
    {
    }

    public X402ExactPermit2Service(
        IAccount facilitatorAccount,
        Dictionary<int, IClient> clientsByChainId,
        Erc20ApprovalGasSponsoringOptions erc20ApprovalGasSponsoring,
        X402FacilitatorOptions options,
        ILogger logger = null)
    {
        _facilitatorAccount = facilitatorAccount ?? throw new ArgumentNullException(nameof(facilitatorAccount));
        _accounts = new FacilitatorAccounts(facilitatorAccount);
        _clientsByChainId = clientsByChainId ?? throw new ArgumentNullException(nameof(clientsByChainId));
        _logger = logger ?? NullLogger.Instance;

        erc20ApprovalGasSponsoring?.Validate();
        options?.Validate();
        _options = options ?? new X402FacilitatorOptions();
        _sender = new FacilitatorTransactionSender(_options);
        _approvalSponsoring = erc20ApprovalGasSponsoring ?? new Erc20ApprovalGasSponsoringOptions();
        _approvalSettlement = _approvalSponsoring.Settlement ?? new Erc20ApprovalSponsoredSettlement(_logger);
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default) =>
        (await VerifyCoreAsync(paymentPayload, requirements, false, cancellationToken)).Verification;

    private async Task<VerifyOutcome> VerifyCoreAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        bool lockApproval,
        CancellationToken cancellationToken)
    {
        Eip2612GasSponsoringInfo sponsoredPermit = null;
        SponsoredApproval sponsoredApproval = null;
        IDisposable? approvalLock = null;
        try
        {
            var preambleRejection = ExactVerification.ProtocolRejection(paymentPayload, requirements, X402AssetTransferMethods.Permit2)
                ?? ExactVerification.AssetRejection(_options, requirements);
            if (preambleRejection != null)
                return Rejected(preambleRejection, null);

            var payload = ExactVerification.ReadPayload<Permit2SchemePayload>(paymentPayload);
            if (payload?.Permit2Authorization == null)
                return Rejected(X402ErrorCodes.InvalidPayload, null);

            var auth = payload.Permit2Authorization;
            var payer = auth.From;

            if (HasMissingFields(payload, requirements))
                return Rejected(X402ErrorCodes.InvalidPayload, null);

            cancellationToken.ThrowIfCancellationRequested();

            if (!Caip2.TryParseEip155ChainId(requirements.Network, out var chainId) ||
                !_clientsByChainId.TryGetValue(chainId, out var client))
                return Rejected(X402ErrorCodes.InvalidNetwork, payer);

            var spenderRejection = SpenderAndRecipientRejection(auth, requirements);
            if (spenderRejection != null)
                return Rejected(spenderRejection, payer);

            var web3 = new Web3.Web3(_accounts.For(chainId), client);

            var witnessMessage = auth.ToWitnessMessage();
            if (!await PayerSignatureCheck.IsValidAsync(
                    web3, payer, payload.Signature.HexToByteArray(),
                    () => TryRecoverSigner(auth, chainId, payload.Signature),
                    () => _signer.ComputeDigest(witnessMessage, chainId, X402Permit2Addresses.Permit2)))
                return Rejected(X402ErrorCodes.Permit2InvalidSignature, payer);

            cancellationToken.ThrowIfCancellationRequested();

            var amount = X402Number.Parse(auth.Permitted.Amount);
            var erc20 = web3.Eth.ERC20.GetContractService(auth.Permitted.Token);

            var verifyBlock = (await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;

            var permit2Allowance = await erc20.AllowanceQueryAsync(payer, X402Permit2Addresses.Permit2);
            cancellationToken.ThrowIfCancellationRequested();
            if (permit2Allowance < amount)
            {
                var sponsoring = await ResolveAllowanceSponsoringAsync(
                    paymentPayload, requirements, web3, payer, chainId, lockApproval, cancellationToken);
                approvalLock = sponsoring.Lock;
                if (sponsoring.Rejection != null)
                    return Rejected(sponsoring.Rejection, payer);

                sponsoredPermit = sponsoring.Permit;
                sponsoredApproval = sponsoring.Approval;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var balance = await erc20.BalanceOfQueryAsync(payer);
            if (balance < amount)
                return Rejected(X402ErrorCodes.Permit2InsufficientBalance, payer);

            var termsRejection = AuthorizedTermsRejection(auth, requirements, amount, sponsoredApproval != null);
            if (termsRejection != null)
                return Rejected(termsRejection, payer);

            if (!string.Equals(paymentPayload.Accepted?.Network, requirements.Network, StringComparison.OrdinalIgnoreCase))
                return Rejected(X402ErrorCodes.NetworkMismatch, payer);

            cancellationToken.ThrowIfCancellationRequested();
            if (!await IsProxyDeployedAsync(web3))
                return Rejected(X402ErrorCodes.Permit2ProxyNotDeployed, payer);

            cancellationToken.ThrowIfCancellationRequested();
            if (await IsNonceUsedAsync(web3, payer, auth))
                return Rejected(X402ErrorCodes.NonceAlreadyUsed, payer);

            cancellationToken.ThrowIfCancellationRequested();
            if (sponsoredPermit != null)
            {
                var simulationFailure = await SimulateSettleWithPermitAsync(
                    web3, auth, payload.Signature, sponsoredPermit, amount, permit2Allowance);
                if (simulationFailure != null)
                    return Rejected(simulationFailure, payer);
            }

            var outcome = new VerifyOutcome(
                new VerificationResponse { IsValid = true, InvalidReason = null, Payer = payer },
                sponsoredPermit,
                sponsoredApproval == null ? null : sponsoredApproval with { Lock = approvalLock },
                verifyBlock);
            approvalLock = null;
            return outcome;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Rejected(ExactVerification.FailureReason(_logger, "exact/permit2", ex), null);
        }
        finally
        {
            approvalLock?.Dispose();
        }
    }

    private static string? SpenderAndRecipientRejection(Permit2Authorization auth, PaymentRequirements requirements)
    {
        if (!auth.Spender.IsTheSameAddress(X402Permit2Addresses.ExactPermit2Proxy))
            return X402ErrorCodes.Permit2InvalidSpender;

        if (!auth.Witness.To.IsTheSameAddress(requirements.PayTo))
            return X402ErrorCodes.Permit2RecipientMismatch;

        return null;
    }

    private string? AuthorizedTermsRejection(
        Permit2Authorization auth, PaymentRequirements requirements, BigInteger amount, bool approvalSponsored)
    {
        if (amount != X402Number.Parse(requirements.Amount))
            return X402ErrorCodes.Permit2AmountMismatch;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deadlineBuffer = approvalSponsored
            ? Math.Max(PaymentAuthorizationValidator.BlockTimeBufferSeconds, _approvalSponsoring.MinimumPermitDeadlineSeconds)
            : PaymentAuthorizationValidator.BlockTimeBufferSeconds;
        if (X402Number.Parse(auth.Deadline) < now + deadlineBuffer)
            return X402ErrorCodes.Permit2DeadlineExpired;

        if (X402Number.Parse(auth.Witness.ValidAfter) > now)
            return X402ErrorCodes.Permit2NotYetValid;

        if (!auth.Permitted.Token.IsTheSameAddress(requirements.Asset))
            return X402ErrorCodes.Permit2TokenMismatch;

        return null;
    }

    private static async Task<bool> IsNonceUsedAsync(Web3.Web3 web3, string payer, Permit2Authorization auth)
    {
        var nonce = X402Number.Parse(auth.Nonce);
        return await FrontRunSettlementRecovery.IsPermit2NonceUsedAsync(web3, payer, nonce);
    }

    private static async Task<bool> IsProxyDeployedAsync(Web3.Web3 web3)
    {
        var proxyCode = await web3.Eth.GetCode.SendRequestAsync(X402Permit2Addresses.ExactPermit2Proxy);
        return !(string.IsNullOrEmpty(proxyCode) || proxyCode == "0x");
    }

    private static bool HasMissingFields(Permit2SchemePayload payload, PaymentRequirements requirements)
    {
        var auth = payload.Permit2Authorization;
        return string.IsNullOrEmpty(payload.Signature) ||
               string.IsNullOrEmpty(auth.From) ||
               string.IsNullOrEmpty(auth.Spender) ||
               string.IsNullOrEmpty(auth.Nonce) ||
               string.IsNullOrEmpty(auth.Deadline) ||
               auth.Permitted == null ||
               string.IsNullOrEmpty(auth.Permitted.Token) ||
               string.IsNullOrEmpty(auth.Permitted.Amount) ||
               auth.Witness == null ||
               string.IsNullOrEmpty(auth.Witness.To) ||
               string.IsNullOrEmpty(auth.Witness.ValidAfter) ||
               string.IsNullOrEmpty(requirements.Amount);
    }

    private readonly record struct AllowanceSponsoring(
        string Rejection,
        Eip2612GasSponsoringInfo Permit,
        SponsoredApproval Approval,
        IDisposable? Lock = null);

    private async Task<AllowanceSponsoring> ResolveAllowanceSponsoringAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        Web3.Web3 web3,
        string payer,
        int chainId,
        bool lockApproval,
        CancellationToken cancellationToken)
    {
        var permitInfo = Eip2612GasSponsoring.Extract(paymentPayload.Extensions);
        if (permitInfo != null)
        {
            var permitRejection = Eip2612GasSponsoring.Validate(
                permitInfo, payer, requirements.Asset, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return permitRejection != null
                ? new AllowanceSponsoring(permitRejection, null, null)
                : new AllowanceSponsoring(null, permitInfo, null);
        }

        var approvalInfo = _approvalSponsoring.Enabled
            ? Erc20ApprovalGasSponsoring.Extract(paymentPayload.Extensions)
            : null;
        if (approvalInfo == null)
            return new AllowanceSponsoring(X402ErrorCodes.Permit2AllowanceRequired, null, null);

        var inspection = Erc20ApprovalGasSponsoring.Inspect(
            approvalInfo, payer, requirements.Asset, chainId,
            X402Number.Parse(requirements.Amount), _approvalSponsoring.MaxSponsoredGasCost);
        if (inspection.Rejection != null)
            return new AllowanceSponsoring(inspection.Rejection, null, null);

        IDisposable? approvalLock = null;
        if (lockApproval)
        {
            if (!X402Address.TryCanonical(payer, out var canonicalPayer))
                return new AllowanceSponsoring(X402ErrorCodes.InvalidPayload, null, null);

            approvalLock = TryLockApproval(chainId, canonicalPayer, inspection.Approval.Nonce);
            if (approvalLock == null)
                return new AllowanceSponsoring(X402ErrorCodes.Erc20ApprovalTxInFlight, null, null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var approvalRejection = await CheckApprovalAgainstNetworkAsync(web3, payer, inspection.Approval)
                ?? await SimulateApprovalAsync(web3, payer, requirements.Asset, approvalInfo, inspection.Approval);
            if (approvalRejection != null)
                return new AllowanceSponsoring(approvalRejection, null, null, approvalLock);

            return new AllowanceSponsoring(
                null, null, new SponsoredApproval(approvalInfo.SignedTransaction, inspection.Approval.GasCost, null), approvalLock);
        }
        catch
        {
            approvalLock?.Dispose();
            throw;
        }
    }

    private IDisposable? TryLockApproval(int chainId, string canonicalPayer, BigInteger nonce)
    {
        var key = $"{chainId}:{canonicalPayer}:{nonce}";
        return _approvalsInFlight.TryAdd(key, 0) ? new ApprovalLock(_approvalsInFlight, key) : null;
    }

    private sealed class ApprovalLock : IDisposable
    {
        private readonly ConcurrentDictionary<string, byte> _held;
        private readonly string _key;
        private int _released;

        public ApprovalLock(ConcurrentDictionary<string, byte> held, string key)
        {
            _held = held;
            _key = key;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                _held.TryRemove(_key, out _);
        }
    }

    private async Task<string> SimulateApprovalAsync(
        Web3.Web3 web3,
        string payer,
        string token,
        Erc20ApprovalGasSponsoringInfo info,
        Erc20ApprovalGasSponsoring.SignedApproval approval)
    {
        try
        {
            var call = new Nethereum.Contracts.Standards.ERC20.ContractDefinition.ApproveFunction { Spender = X402Permit2Addresses.Permit2, Value = X402Number.Parse(info.Amount) }
                .CreateTransactionInput(token);
            call.From = payer;
            var estimate = await web3.Eth.TransactionManager.EstimateGasAsync(call);
            if (estimate.Value > approval.GasLimit)
                return X402ErrorCodes.Erc20ApprovalTxGasLimitTooLow;

            return approval.GasLimit > Erc20ApprovalGasSponsoring.MaximumGasLimit(estimate.Value)
                ? X402ErrorCodes.Erc20ApprovalTxGasLimitTooHigh
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "x402 exact/permit2 sponsored approval simulation failed for payer {Payer}", payer);
            return X402ErrorCodes.Erc20ApprovalTxSimulationFailed;
        }
    }

    public async Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        var (verification, sponsoredPermit, sponsoredApproval, verifyBlock) =
            await VerifyCoreAsync(paymentPayload, requirements, true, cancellationToken);
        if (!verification.IsValid)
            return SettlementResponse.Failure(requirements.Network, verification.InvalidReason, verification.Payer);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payload = ExactVerification.ReadPayload<Permit2SchemePayload>(paymentPayload);
            var auth = payload.Permit2Authorization;
            Caip2.TryParseEip155ChainId(requirements.Network, out var chainId);
            var client = _clientsByChainId[chainId];
            var web3 = new Web3.Web3(_accounts.For(chainId), client);

            var proxyHandler = web3.Eth.GetContractHandler(X402Permit2Addresses.ExactPermit2Proxy);

            if (sponsoredApproval != null)
                return await _approvalSettlement.SettleAsync(
                    new Erc20ApprovalSponsoredSettlementRequest(
                        web3,
                        requirements.Network,
                        auth.From,
                        sponsoredApproval.SignedTransaction,
                        sponsoredApproval.GasCost,
                        _ => ExecuteSettleAsync(
                            web3, proxyHandler, auth.ToSettleFunction(payload.Signature), requirements, auth, verifyBlock))
                    {
                        MaxTimeoutSeconds = requirements.MaxTimeoutSeconds,
                        Sender = _sender
                    },
                    cancellationToken);

            if (sponsoredPermit == null)
                return await ExecuteSettleAsync(
                    web3, proxyHandler, auth.ToSettleFunction(payload.Signature), requirements, auth, verifyBlock);

            var settleWithPermit = auth.ToSettleWithPermitFunction(
                payload.Signature, Eip2612GasSponsoring.ToPermitParams(sponsoredPermit));
            return await ExecuteSettleAsync(web3, proxyHandler, settleWithPermit, requirements, auth, verifyBlock);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (X402SettlementGasExceededException ex)
        {
            _logger.LogWarning(ex, "x402 exact/permit2 settlement for payer {Payer} refused: gas estimate above the ceiling", verification.Payer);
            return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.SettlementGasExceedsLimit, verification.Payer);
        }
        catch (X402SettlementPendingException ex)
        {
            _logger.LogWarning(ex, "x402 exact/permit2 settlement {Transaction} broadcast but receipt not obtained for payer {Payer}", ex.TransactionHash, verification.Payer);
            return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.SettlementPending, verification.Payer, ex.TransactionHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "x402 exact/permit2 settle failed for payer {Payer}", verification.Payer);
            return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.UnexpectedSettleError, verification.Payer);
        }
        finally
        {
            sponsoredApproval?.Lock?.Dispose();
        }
    }

    private async Task<SettlementResponse> ExecuteSettleAsync<TFunction>(
        Web3.Web3 web3,
        ContractHandler proxyHandler,
        TFunction settle,
        PaymentRequirements requirements,
        Permit2Authorization auth,
        BigInteger verifyBlock)
        where TFunction : FunctionMessage, new()
    {
        using var receiptBound = new SettlementBound(_sender.Bound(requirements.MaxTimeoutSeconds));
        BigInteger estimate;
        try
        {
            estimate = (await proxyHandler.EstimateGasAsync(settle)).Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var settledByOther = await FindSettledByOtherAsync(web3, requirements, auth, verifyBlock);
            if (settledByOther != null)
                return await ConfirmedBySettlerAsync(settledByOther, web3, receiptBound, requirements, auth);

            var reason = ex is SmartContractCustomErrorRevertException custom
                ? Permit2SettlementErrorMapper.MapReason(custom) ?? X402ErrorCodes.TransactionSimulationFailed
                : X402ErrorCodes.TransactionSimulationFailed;
            _logger.LogWarning(ex, "x402 exact/permit2 settle simulation reverted ({Reason}) for payer {Payer}", reason, auth.From);
            return SettlementResponse.Failure(requirements.Network, reason, auth.From);
        }

        settle.Gas = _sender.GasLimitFor(estimate);

        TransactionReceipt receipt;
        try
        {
            receipt = await _sender.SendAndWaitForReceiptAsync(
                web3, proxyHandler, settle, receiptBound);
        }
        catch (Exception ex) when (ex is not X402SettlementPendingException
                                   and not X402SettlementGasExceededException
                                   and not OperationCanceledException)
        {
            var settledByOther = await FindSettledByOtherAsync(web3, requirements, auth, verifyBlock);
            if (settledByOther == null)
                throw;

            _logger.LogWarning(ex, "x402 exact/permit2 settlement for payer {Payer} reverted; the payment was already settled by {Transaction}", auth.From, settledByOther.Receipt.TransactionHash);
            return await ConfirmedBySettlerAsync(settledByOther, web3, receiptBound, requirements, auth);
        }

        if (receipt.Status.Value == 1)
        {
            if (!SettlementTransferCheck.HasExactlyOneMatchingTransfer(
                    receipt, requirements.Asset, auth.From, requirements.PayTo, X402Number.Parse(requirements.Amount)))
                return SettlementResponse.Failure(requirements.Network, X402ErrorCodes.TransferEventMismatch, auth.From, receipt.TransactionHash);

            return SettlementResponse.Succeeded(requirements.Network, auth.From, receipt.TransactionHash, auth.Permitted.Amount);
        }

        var otherSettlement = await FindSettledByOtherAsync(web3, requirements, auth, verifyBlock);
        if (otherSettlement != null)
            return await ConfirmedBySettlerAsync(otherSettlement, web3, receiptBound, requirements, auth);

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
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "x402 exact/permit2 revert reason read failed for payer {Payer}", auth.From);
        }

        return SettlementResponse.Failure(requirements.Network, revertReason, auth.From, receipt.TransactionHash);
    }

    private async Task<SettlementResponse> ConfirmedBySettlerAsync(
        RecoveredSettlement settledByOther,
        Web3.Web3 web3,
        SettlementBound receiptBound,
        PaymentRequirements requirements,
        Permit2Authorization auth)
    {
        var receipt = await settledByOther.ConfirmAsync(_sender, web3, receiptBound);
        return SettlementResponse.Succeeded(requirements.Network, auth.From, receipt.TransactionHash, auth.Permitted.Amount);
    }

    private async Task<RecoveredSettlement?> FindSettledByOtherAsync(
        Web3.Web3 web3,
        PaymentRequirements requirements,
        Permit2Authorization auth,
        BigInteger verifyBlock)
    {
        try
        {
            return await FrontRunSettlementRecovery.FindPermit2SettlementAsync(
                web3, requirements.Asset, auth.From, X402Number.Parse(auth.Nonce),
                requirements.PayTo, X402Number.Parse(requirements.Amount), verifyBlock,
                new RecoveryScope(
                    Caip2.ParseEip155ChainId(requirements.Network), web3.TransactionManager.Account.Address, X402Number.Parse(auth.Deadline), _options.FacilitatorAddresses));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "x402 exact/permit2 front-run settlement lookup failed for payer {Payer}", auth.From);
            return null;
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
        return Task.FromResult(new SupportedPaymentKindsResponse
        {
            Kinds = kinds,
            Extensions = SupportedExtensions(),
            Signers = SupportedSigners.ForAccount(_facilitatorAccount.Address)
        });
    }

    private string TryRecoverSigner(Permit2Authorization authorization, int chainId, string signature)
    {
        try
        {
            return _signer.RecoverSigner(authorization.ToWitnessMessage(), chainId, X402Permit2Addresses.Permit2, signature);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "x402 exact/permit2 verify: the signature does not recover");
            return null;
        }
    }

    private List<string> SupportedExtensions()
    {
        var extensions = new List<string> { X402ExtensionKeys.Eip2612GasSponsoring };
        if (_approvalSponsoring.Enabled)
            extensions.Add(X402ExtensionKeys.Erc20ApprovalGasSponsoring);
        return extensions;
    }

    private static async Task<BigInteger> SuggestedPriorityFeeAsync(Web3.Web3 web3)
    {
        try
        {
            return (await new Nethereum.RPC.Eth.EthMaxPriorityFeePerGas(web3.Client).SendRequestAsync()).Value;
        }
        catch (Exception)
        {
            return BigInteger.Zero;
        }
    }

    private static async Task<string> CheckApprovalAgainstNetworkAsync(
        Web3.Web3 web3,
        string payer,
        Erc20ApprovalGasSponsoring.SignedApproval approval)
    {
        var pendingNonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(payer, BlockParameter.CreatePending());
        if (approval.Nonce != pendingNonce.Value)
            return X402ErrorCodes.Erc20ApprovalTxNonceMismatch;

        var latest = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());
        if (latest?.GasLimit != null &&
            approval.GasLimit > BigInteger.Min(latest.GasLimit.Value, Erc20ApprovalGasSponsoring.MaxApprovalGasLimit))
            return X402ErrorCodes.Erc20ApprovalTxGasLimitTooHigh;

        if (latest?.BaseFeePerGas != null &&
            approval.MaxFeePerGas < Erc20ApprovalGasSponsoring.MinimumMaxFeePerGas(latest.BaseFeePerGas.Value))
            return X402ErrorCodes.Erc20ApprovalTxMaxFeeBelowBaseFee;

        if (approval.MaxPriorityFeePerGas < BigInteger.One)
            return X402ErrorCodes.Erc20ApprovalTxPriorityFeeTooLow;

        if (approval.MaxPriorityFeePerGas > Erc20ApprovalGasSponsoring.MaximumPriorityFee(await SuggestedPriorityFeeAsync(web3)))
            return X402ErrorCodes.Erc20ApprovalTxPriorityFeeTooHigh;

        if (latest?.BaseFeePerGas != null &&
            approval.MaxFeePerGas > Erc20ApprovalGasSponsoring.MaximumMaxFeePerGas(latest.BaseFeePerGas.Value, approval.MaxPriorityFeePerGas))
            return X402ErrorCodes.Erc20ApprovalTxMaxFeeTooHigh;

        return null;
    }

    private async Task<string> SimulateSettleWithPermitAsync(
        Web3.Web3 web3,
        Permit2Authorization auth,
        string signature,
        Eip2612GasSponsoringInfo permitInfo,
        BigInteger amount,
        BigInteger permit2Allowance)
    {
        try
        {
            var settleWithPermit = auth.ToSettleWithPermitFunction(signature, Eip2612GasSponsoring.ToPermitParams(permitInfo));
            await web3.Eth.GetContractHandler(X402Permit2Addresses.ExactPermit2Proxy).QueryRawAsync(settleWithPermit);
            return null;
        }
        catch (SmartContractCustomErrorRevertException ex) when (ex.IsCustomErrorFor<Permit2612AmountMismatchError>())
        {
            _logger.LogDebug(ex, "x402 exact/permit2 settleWithPermit simulation: permit value differs from the permitted amount for payer {Payer}", auth.From);
            return X402ErrorCodes.Permit2612AmountMismatch;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "x402 exact/permit2 settleWithPermit simulation failed for payer {Payer}", auth.From);
            return await DiagnoseSimulationFailureAsync(web3, auth.From, auth.Permitted.Token, amount, permit2Allowance);
        }
    }

    private async Task<string> DiagnoseSimulationFailureAsync(
        Web3.Web3 web3,
        string payer,
        string token,
        BigInteger amount,
        BigInteger permit2Allowance)
    {
        try
        {
            if (!await IsProxyDeployedAsync(web3))
                return X402ErrorCodes.Permit2ProxyNotDeployed;

            var erc20 = web3.Eth.ERC20.GetContractService(token);
            if (await erc20.BalanceOfQueryAsync(payer) < amount)
                return X402ErrorCodes.Permit2InsufficientBalance;

            if (permit2Allowance < amount)
                return X402ErrorCodes.Permit2AllowanceRequired;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "x402 exact/permit2 simulation failure diagnosis failed for payer {Payer}", payer);
        }

        return X402ErrorCodes.Permit2SimulationFailed;
    }

    private static VerifyOutcome Rejected(string reason, string payer) => new(Invalid(reason, payer), null, null, BigInteger.Zero);

    private sealed record SponsoredApproval(string SignedTransaction, BigInteger GasCost, IDisposable? Lock);

    private readonly record struct VerifyOutcome(
        VerificationResponse Verification,
        Eip2612GasSponsoringInfo SponsoredPermit,
        SponsoredApproval SponsoredApproval,
        BigInteger VerifyBlock);

    private static VerificationResponse Invalid(string reason, string payer) =>
        new() { IsValid = false, InvalidReason = reason, Payer = payer };
}
