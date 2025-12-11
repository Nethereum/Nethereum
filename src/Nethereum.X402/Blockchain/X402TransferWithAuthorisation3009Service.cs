using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.EIP3009.EIP3009;
using Nethereum.Contracts.EIP3009.EIP3009.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;

namespace Nethereum.X402.Blockchain;

public class X402TransferWithAuthorisation3009Service : IX402PaymentProcessor
{
    private readonly IAccount _facilitatorAccount;
    private readonly Dictionary<string, string> _rpcEndpoints;
    private readonly Dictionary<string, string> _tokenAddresses;
    private readonly Dictionary<string, int> _chainIds;
    private readonly Dictionary<string, string> _tokenNames;
    private readonly Dictionary<string, string> _tokenVersions;
    private readonly PaymentAuthorizationValidator _validator;

    public X402TransferWithAuthorisation3009Service(
        string facilitatorPrivateKey,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
        : this(new Account(facilitatorPrivateKey), rpcEndpoints, tokenAddresses, chainIds, tokenNames, tokenVersions)
    {
    }

    public X402TransferWithAuthorisation3009Service(
        IAccount facilitatorAccount,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
    {
        _facilitatorAccount = facilitatorAccount ?? throw new ArgumentNullException(nameof(facilitatorAccount));
        _rpcEndpoints = rpcEndpoints ?? throw new ArgumentNullException(nameof(rpcEndpoints));
        _tokenAddresses = tokenAddresses ?? throw new ArgumentNullException(nameof(tokenAddresses));
        _chainIds = chainIds ?? throw new ArgumentNullException(nameof(chainIds));
        _tokenNames = tokenNames ?? throw new ArgumentNullException(nameof(tokenNames));
        _tokenVersions = tokenVersions ?? throw new ArgumentNullException(nameof(tokenVersions));
        _validator = new PaymentAuthorizationValidator();
    }

    public async Task<VerificationResponse> VerifyPaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (paymentPayload.Scheme != "exact")
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = X402ErrorCodes.UnsupportedScheme,
                    Payer = null
                };
            }

            var exactPayload = GetExactSchemePayload(paymentPayload);
            if (exactPayload == null)
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = X402ErrorCodes.InvalidPayload,
                    Payer = null
                };
            }

            var authorization = exactPayload.Authorization;
            var signatureHex = exactPayload.Signature;

            if (!ValidateNetworkConfiguration(requirements.Network, out var configError))
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = configError,
                    Payer = authorization.From
                };
            }

            var validationResult = await _validator.ValidateAsync(
                authorization,
                signatureHex,
                _tokenAddresses[requirements.Network],
                _tokenNames[requirements.Network],
                _tokenVersions[requirements.Network],
                _chainIds[requirements.Network],
                _rpcEndpoints[requirements.Network],
                cancellationToken
            );

            return new VerificationResponse
            {
                IsValid = validationResult.IsValid,
                InvalidReason = validationResult.InvalidReason,
                Payer = authorization.From
            };
        }
        catch (Exception ex)
        {
            return new VerificationResponse
            {
                IsValid = false,
                InvalidReason = X402ErrorCodes.UnexpectedVerifyError,
                Payer = null
            };
        }
    }

    public async Task<SettlementResponse> SettlePaymentAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        var verificationResult = await VerifyPaymentAsync(paymentPayload, requirements, cancellationToken);

        if (!verificationResult.IsValid)
        {
            return new SettlementResponse
            {
                Success = false,
                ErrorReason = verificationResult.InvalidReason,
                Transaction = null,
                Network = requirements.Network,
                Payer = verificationResult.Payer
            };
        }

        try
        {
            var exactPayload = GetExactSchemePayload(paymentPayload);
            var authorization = exactPayload.Authorization;
            var signatureHex = exactPayload.Signature;

            var rpcUrl = _rpcEndpoints[requirements.Network];
            var tokenAddress = _tokenAddresses[requirements.Network];

            var web3 = new Nethereum.Web3.Web3(_facilitatorAccount, rpcUrl);
            var eip3009Service = new Eip3009Service(web3, tokenAddress);

            var signature = EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);

            var transferFunction = new TransferWithAuthorization1Function
            {
                AuthorisationFrom = authorization.From,
                AuthorisationTo = authorization.To,
                Value = BigInteger.Parse(authorization.Value),
                ValidAfter = BigInteger.Parse(authorization.ValidAfter),
                ValidBefore = BigInteger.Parse(authorization.ValidBefore),
                AuthorisationNonce = authorization.Nonce.HexToByteArray(),
                V = signature.V[0],
                R = signature.R,
                S = signature.S
            };

            var cancellationTokenSource = cancellationToken != default
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            var receipt = await eip3009Service.TransferWithAuthorizationRequestAndWaitForReceiptAsync(
                transferFunction,
                cancellationTokenSource
            );

            if (receipt.Status.Value == 1)
            {
                return new SettlementResponse
                {
                    Success = true,
                    ErrorReason = null,
                    Transaction = receipt.TransactionHash,
                    Network = requirements.Network,
                    Payer = authorization.From
                };
            }
            else
            {
                return new SettlementResponse
                {
                    Success = false,
                    ErrorReason = X402ErrorCodes.InvalidTransactionState,
                    Transaction = receipt.TransactionHash,
                    Network = requirements.Network,
                    Payer = authorization.From
                };
            }
        }
        catch (Exception ex)
        {
            return new SettlementResponse
            {
                Success = false,
                ErrorReason = X402ErrorCodes.UnexpectedSettleError,
                Transaction = null,
                Network = requirements.Network,
                Payer = verificationResult.Payer
            };
        }
    }

    public async Task<CancelAuthorizationResponse> CancelAuthorizationAsync(
        string authorizerAddress,
        byte[] nonce,
        string network,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ValidateNetworkConfiguration(network, out var configError))
            {
                return new CancelAuthorizationResponse
                {
                    Success = false,
                    ErrorReason = configError,
                    Transaction = null,
                    Network = network
                };
            }

            var rpcUrl = _rpcEndpoints[network];
            var tokenAddress = _tokenAddresses[network];

            var web3 = new Nethereum.Web3.Web3(_facilitatorAccount, rpcUrl);
            var eip3009Service = new Eip3009Service(web3, tokenAddress);

            var cancelFunction = new CancelAuthorization1Function
            {
                Authorizer = authorizerAddress,
                AuthorisationNonce = nonce,
                V = 0,
                R = new byte[32],
                S = new byte[32]
            };

            var cancellationTokenSource = cancellationToken != default
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            var receipt = await eip3009Service.CancelAuthorizationRequestAndWaitForReceiptAsync(
                cancelFunction,
                cancellationTokenSource
            );

            return new CancelAuthorizationResponse
            {
                Success = receipt.Status.Value == 1,
                ErrorReason = receipt.Status.Value == 1 ? null : "Transaction reverted",
                Transaction = receipt.TransactionHash,
                Network = network
            };
        }
        catch (Exception ex)
        {
            return new CancelAuthorizationResponse
            {
                Success = false,
                ErrorReason = $"Cancellation error: {ex.Message}",
                Transaction = null,
                Network = network
            };
        }
    }

    public Task<SupportedPaymentKindsResponse> GetSupportedAsync(
        CancellationToken cancellationToken = default)
    {
        var supportedKinds = new List<PaymentKind>();

        foreach (var network in _rpcEndpoints.Keys)
        {
            supportedKinds.Add(new PaymentKind
            {
                X402Version = 1,
                Scheme = "exact",
                Network = network,
                Extra = null
            });
        }

        return Task.FromResult(new SupportedPaymentKindsResponse
        {
            Kinds = supportedKinds
        });
    }

    private bool ValidateNetworkConfiguration(string network, out string error)
    {
        if (!_chainIds.ContainsKey(network))
        {
            error = X402ErrorCodes.InvalidNetwork;
            return false;
        }

        if (!_tokenAddresses.ContainsKey(network))
        {
            error = X402ErrorCodes.InvalidNetwork;
            return false;
        }

        if (!_tokenNames.ContainsKey(network))
        {
            error = X402ErrorCodes.InvalidNetwork;
            return false;
        }

        if (!_tokenVersions.ContainsKey(network))
        {
            error = X402ErrorCodes.InvalidNetwork;
            return false;
        }

        if (!_rpcEndpoints.ContainsKey(network))
        {
            error = X402ErrorCodes.InvalidNetwork;
            return false;
        }

        error = null;
        return true;
    }

    private ExactSchemePayload GetExactSchemePayload(PaymentPayload paymentPayload)
    {
        if (paymentPayload.Payload is ExactSchemePayload exactPayload)
        {
            return exactPayload;
        }

        if (paymentPayload.Payload is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<ExactSchemePayload>(jsonElement.GetRawText());
        }

        return null;
    }
}

public class CancelAuthorizationResponse
{
    public bool Success { get; set; }
    public string ErrorReason { get; set; }
    public string Transaction { get; set; }
    public string Network { get; set; }
}
