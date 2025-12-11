using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.EIP3009.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Standards.EIP3009
{
    /// <summary>
    /// EIP-3009: Transfer With Authorization Contract Service
    /// Service to interact with smart contracts implementing the EIP-3009 standard
    /// https://eips.ethereum.org/EIPS/eip-3009
    /// </summary>
    public class EIP3009ContractService
    {
        public string ContractAddress { get; }

        public ContractHandler ContractHandler { get; }

        public EIP3009ContractService(IEthApiContractService ethApiContractService, string contractAddress)
        {
            ContractAddress = contractAddress;
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }

        public Event<AuthorizationUsedEventDTO> GetAuthorizationUsedEvent()
        {
            return ContractHandler.GetEvent<AuthorizationUsedEventDTO>();
        }

        public Event<AuthorizationCanceledEventDTO> GetAuthorizationCanceledEvent()
        {
            return ContractHandler.GetEvent<AuthorizationCanceledEventDTO>();
        }

#if !DOTNET35
        // TransferWithAuthorization methods
        public Task<string> TransferWithAuthorizationRequestAsync(TransferWithAuthorizationFunction transferWithAuthorizationFunction)
        {
            return ContractHandler.SendRequestAsync(transferWithAuthorizationFunction);
        }

        public Task<TransactionReceipt> TransferWithAuthorizationRequestAndWaitForReceiptAsync(
            TransferWithAuthorizationFunction transferWithAuthorizationFunction,
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferWithAuthorizationFunction, cancellationToken);
        }

        public Task<string> TransferWithAuthorizationRequestAsync(
            string from, string to, BigInteger value, BigInteger validAfter, BigInteger validBefore,
            byte[] nonce, byte v, byte[] r, byte[] s)
        {
            var transferWithAuthorizationFunction = new TransferWithAuthorizationFunction
            {
                From = from,
                To = to,
                Value = value,
                ValidAfter = validAfter,
                ValidBefore = validBefore,
                Nonce = nonce,
                V = v,
                R = r,
                S = s
            };

            return ContractHandler.SendRequestAsync(transferWithAuthorizationFunction);
        }

        public Task<TransactionReceipt> TransferWithAuthorizationRequestAndWaitForReceiptAsync(
            string from, string to, BigInteger value, BigInteger validAfter, BigInteger validBefore,
            byte[] nonce, byte v, byte[] r, byte[] s,
            CancellationToken cancellationToken = default)
        {
            var transferWithAuthorizationFunction = new TransferWithAuthorizationFunction
            {
                From = from,
                To = to,
                Value = value,
                ValidAfter = validAfter,
                ValidBefore = validBefore,
                Nonce = nonce,
                V = v,
                R = r,
                S = s
            };

            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferWithAuthorizationFunction, cancellationToken);
        }

        // ReceiveWithAuthorization methods
        public Task<string> ReceiveWithAuthorizationRequestAsync(ReceiveWithAuthorizationFunction receiveWithAuthorizationFunction)
        {
            return ContractHandler.SendRequestAsync(receiveWithAuthorizationFunction);
        }

        public Task<TransactionReceipt> ReceiveWithAuthorizationRequestAndWaitForReceiptAsync(
            ReceiveWithAuthorizationFunction receiveWithAuthorizationFunction,
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(receiveWithAuthorizationFunction, cancellationToken);
        }

        public Task<string> ReceiveWithAuthorizationRequestAsync(
            string from, string to, BigInteger value, BigInteger validAfter, BigInteger validBefore,
            byte[] nonce, byte v, byte[] r, byte[] s)
        {
            var receiveWithAuthorizationFunction = new ReceiveWithAuthorizationFunction
            {
                From = from,
                To = to,
                Value = value,
                ValidAfter = validAfter,
                ValidBefore = validBefore,
                Nonce = nonce,
                V = v,
                R = r,
                S = s
            };

            return ContractHandler.SendRequestAsync(receiveWithAuthorizationFunction);
        }

        public Task<TransactionReceipt> ReceiveWithAuthorizationRequestAndWaitForReceiptAsync(
            string from, string to, BigInteger value, BigInteger validAfter, BigInteger validBefore,
            byte[] nonce, byte v, byte[] r, byte[] s,
            CancellationToken cancellationToken = default)
        {
            var receiveWithAuthorizationFunction = new ReceiveWithAuthorizationFunction
            {
                From = from,
                To = to,
                Value = value,
                ValidAfter = validAfter,
                ValidBefore = validBefore,
                Nonce = nonce,
                V = v,
                R = r,
                S = s
            };

            return ContractHandler.SendRequestAndWaitForReceiptAsync(receiveWithAuthorizationFunction, cancellationToken);
        }

        // CancelAuthorization methods
        public Task<string> CancelAuthorizationRequestAsync(CancelAuthorizationFunction cancelAuthorizationFunction)
        {
            return ContractHandler.SendRequestAsync(cancelAuthorizationFunction);
        }

        public Task<TransactionReceipt> CancelAuthorizationRequestAndWaitForReceiptAsync(
            CancelAuthorizationFunction cancelAuthorizationFunction,
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(cancelAuthorizationFunction, cancellationToken);
        }

        public Task<string> CancelAuthorizationRequestAsync(
            string authorizer, byte[] nonce, byte v, byte[] r, byte[] s)
        {
            var cancelAuthorizationFunction = new CancelAuthorizationFunction
            {
                Authorizer = authorizer,
                Nonce = nonce,
                V = v,
                R = r,
                S = s
            };

            return ContractHandler.SendRequestAsync(cancelAuthorizationFunction);
        }

        public Task<TransactionReceipt> CancelAuthorizationRequestAndWaitForReceiptAsync(
            string authorizer, byte[] nonce, byte v, byte[] r, byte[] s,
            CancellationToken cancellationToken = default)
        {
            var cancelAuthorizationFunction = new CancelAuthorizationFunction
            {
                Authorizer = authorizer,
                Nonce = nonce,
                V = v,
                R = r,
                S = s
            };

            return ContractHandler.SendRequestAndWaitForReceiptAsync(cancelAuthorizationFunction, cancellationToken);
        }

        // AuthorizationState query methods
        public Task<bool> AuthorizationStateQueryAsync(AuthorizationStateFunction authorizationStateFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<AuthorizationStateFunction, AuthorizationStateOutputDTO>(
                authorizationStateFunction, blockParameter).ContinueWith(t => t.Result.IsUsed);
        }

        public Task<bool> AuthorizationStateQueryAsync(string authorizer, byte[] nonce,
            BlockParameter blockParameter = null)
        {
            var authorizationStateFunction = new AuthorizationStateFunction
            {
                Authorizer = authorizer,
                Nonce = nonce
            };

            return AuthorizationStateQueryAsync(authorizationStateFunction, blockParameter);
        }
#endif
    }
}
