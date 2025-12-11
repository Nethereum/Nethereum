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
using Nethereum.X402.Signers;

namespace Nethereum.X402.Blockchain;

public class X402ReceiveWithAuthorisation3009Service : IX402PaymentProcessor
{
    private readonly IAccount _receiverAccount;
    private readonly Dictionary<string, string> _rpcEndpoints;
    private readonly Dictionary<string, string> _tokenAddresses;
    private readonly Dictionary<string, int> _chainIds;
    private readonly Dictionary<string, string> _tokenNames;
    private readonly Dictionary<string, string> _tokenVersions;
    private readonly TransferWithAuthorisationSigner _signer;

    public X402ReceiveWithAuthorisation3009Service(
        string receiverPrivateKey,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
        : this(new Account(receiverPrivateKey), rpcEndpoints, tokenAddresses, chainIds, tokenNames, tokenVersions)
    {
    }

    public X402ReceiveWithAuthorisation3009Service(
        IAccount receiverAccount,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
    {
        _receiverAccount = receiverAccount ?? throw new ArgumentNullException(nameof(receiverAccount));
        _rpcEndpoints = rpcEndpoints ?? throw new ArgumentNullException(nameof(rpcEndpoints));
        _tokenAddresses = tokenAddresses ?? throw new ArgumentNullException(nameof(tokenAddresses));
        _chainIds = chainIds ?? throw new ArgumentNullException(nameof(chainIds));
        _tokenNames = tokenNames ?? throw new ArgumentNullException(nameof(tokenNames));
        _tokenVersions = tokenVersions ?? throw new ArgumentNullException(nameof(tokenVersions));
        _signer = new TransferWithAuthorisationSigner();
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

            if (!authorization.To.Equals(_receiverAccount.Address, StringComparison.OrdinalIgnoreCase))
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = X402ErrorCodes.RecipientMismatch,
                    Payer = authorization.From
                };
            }

            if (!ValidateNetworkConfiguration(requirements.Network, out var configError))
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = configError,
                    Payer = authorization.From
                };
            }

            // Verify signature using ReceiveWithAuthorization
            var signature = EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);
            var recoveredAddress = _signer.RecoverReceiveAddress(
                authorization,
                _tokenNames[requirements.Network],
                _tokenVersions[requirements.Network],
                _chainIds[requirements.Network],
                _tokenAddresses[requirements.Network],
                signature
            );

            if (!authorization.From.Equals(recoveredAddress, StringComparison.OrdinalIgnoreCase))
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = X402ErrorCodes.InvalidSignature,
                    Payer = authorization.From
                };
            }

            var rpcUrl = _rpcEndpoints[requirements.Network];
            var tokenAddress = _tokenAddresses[requirements.Network];
            var web3 = new Nethereum.Web3.Web3(rpcUrl);
            var erc20Service = web3.Eth.ERC20.GetContractService(tokenAddress);

            var balance = await erc20Service.BalanceOfQueryAsync(authorization.From);
            var requiredValue = BigInteger.Parse(authorization.Value);

            if (balance < requiredValue)
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = X402ErrorCodes.InsufficientFunds,
                    Payer = authorization.From
                };
            }

            var eip3009Service = new Eip3009Service(web3, tokenAddress);
            var nonceBytes = authorization.Nonce.HexToByteArray();
            var nonceState = await eip3009Service.AuthorizationStateQueryAsync(
                authorization.From,
                nonceBytes
            );

            if (nonceState)
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = X402ErrorCodes.NonceAlreadyUsed,
                    Payer = authorization.From
                };
            }

            var latestBlock = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(
                BlockParameter.CreateLatest()
            );
            var blockTimestamp = (long)latestBlock.Timestamp.Value;
            var validAfter = BigInteger.Parse(authorization.ValidAfter);
            var validBefore = BigInteger.Parse(authorization.ValidBefore);

            if (blockTimestamp < validAfter)
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = X402ErrorCodes.InvalidValidAfter,
                    Payer = authorization.From
                };
            }

            if (blockTimestamp > validBefore)
            {
                return new VerificationResponse
                {
                    IsValid = false,
                    InvalidReason = X402ErrorCodes.InvalidValidBefore,
                    Payer = authorization.From
                };
            }

            return new VerificationResponse
            {
                IsValid = true,
                InvalidReason = null,
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

            var web3 = new Nethereum.Web3.Web3(_receiverAccount, rpcUrl);
            var eip3009Service = new Eip3009Service(web3, tokenAddress);

            var signature = EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);

            // Use ReceiveWithAuthorization1Function
            var receiveFunction = new ReceiveWithAuthorization1Function
            {
                From = authorization.From,
                To = authorization.To,
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

            var receipt = await eip3009Service.ReceiveWithAuthorizationRequestAndWaitForReceiptAsync(
                receiveFunction,
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
