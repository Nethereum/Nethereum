using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ChainStateVerification.Interceptor
{
    public class VerifiedStateInterceptor : RequestInterceptor
    {
        private readonly IVerifiedStateService _verifiedStateService;
        private readonly HashSet<string> _enabledMethods;

        public bool FallbackOnError { get; set; } = true;

        public event EventHandler<VerificationFallbackEventArgs> FallbackTriggered;

        public static readonly HashSet<string> DefaultEnabledMethods = new HashSet<string>
        {
            ApiMethods.eth_getBalance.ToString(),
            ApiMethods.eth_getTransactionCount.ToString(),
            ApiMethods.eth_getCode.ToString(),
            ApiMethods.eth_getStorageAt.ToString(),
            ApiMethods.eth_blockNumber.ToString()
        };

        public VerifiedStateInterceptor(IVerifiedStateService verifiedStateService)
            : this(verifiedStateService, DefaultEnabledMethods)
        {
        }

        public VerifiedStateInterceptor(IVerifiedStateService verifiedStateService, HashSet<string> enabledMethods)
        {
            _verifiedStateService = verifiedStateService ?? throw new ArgumentNullException(nameof(verifiedStateService));
            _enabledMethods = enabledMethods ?? DefaultEnabledMethods;
        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<RpcRequest, string, Task<T>> interceptedSendRequestAsync,
            RpcRequest request,
            string route = null)
        {
            if (!_enabledMethods.Contains(request.Method))
            {
                return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, request, route)
                    .ConfigureAwait(false);
            }

            try
            {
                var result = await HandleVerifiedRequestAsync<T>(request.Method, request.RawParameters)
                    .ConfigureAwait(false);

                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception ex) when (FallbackOnError)
            {
                OnFallbackTriggered(request.Method, ex);
            }

            return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, request, route)
                .ConfigureAwait(false);
        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<string, string, object[], Task<T>> interceptedSendRequestAsync,
            string method,
            string route = null,
            params object[] paramList)
        {
            if (!_enabledMethods.Contains(method))
            {
                return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, method, route, paramList)
                    .ConfigureAwait(false);
            }

            try
            {
                var result = await HandleVerifiedRequestAsync<T>(method, paramList)
                    .ConfigureAwait(false);

                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception ex) when (FallbackOnError)
            {
                OnFallbackTriggered(method, ex);
            }

            return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, method, route, paramList)
                .ConfigureAwait(false);
        }

        private async Task<object> HandleVerifiedRequestAsync<T>(string method, object[] parameters)
        {
            switch (method)
            {
                case "eth_getBalance":
                    return await HandleGetBalanceAsync(parameters).ConfigureAwait(false);

                case "eth_getTransactionCount":
                    return await HandleGetTransactionCountAsync(parameters).ConfigureAwait(false);

                case "eth_getCode":
                    return await HandleGetCodeAsync(parameters).ConfigureAwait(false);

                case "eth_getStorageAt":
                    return await HandleGetStorageAtAsync(parameters).ConfigureAwait(false);

                case "eth_blockNumber":
                    return HandleGetBlockNumber();

                default:
                    return null;
            }
        }

        private async Task<object> HandleGetBalanceAsync(object[] parameters)
        {
            if (parameters == null || parameters.Length < 1)
                return null;

            var address = GetAddressParameter(parameters[0]);
            if (string.IsNullOrEmpty(address))
                return null;

            var blockParameter = GetBlockParameter(parameters, 1);
            if (!IsCurrentBlock(blockParameter))
                return null;

            var balance = await _verifiedStateService.GetBalanceAsync(address).ConfigureAwait(false);
            return new HexBigInteger(balance);
        }

        private async Task<object> HandleGetTransactionCountAsync(object[] parameters)
        {
            if (parameters == null || parameters.Length < 1)
                return null;

            var address = GetAddressParameter(parameters[0]);
            if (string.IsNullOrEmpty(address))
                return null;

            var blockParameter = GetBlockParameter(parameters, 1);
            if (!IsCurrentBlock(blockParameter))
                return null;

            var nonce = await _verifiedStateService.GetNonceAsync(address).ConfigureAwait(false);
            return new HexBigInteger(nonce);
        }

        private async Task<object> HandleGetCodeAsync(object[] parameters)
        {
            if (parameters == null || parameters.Length < 1)
                return null;

            var address = GetAddressParameter(parameters[0]);
            if (string.IsNullOrEmpty(address))
                return null;

            var blockParameter = GetBlockParameter(parameters, 1);
            if (!IsCurrentBlock(blockParameter))
                return null;

            var code = await _verifiedStateService.GetCodeAsync(address).ConfigureAwait(false);
            return code.ToHex(true);
        }

        private async Task<object> HandleGetStorageAtAsync(object[] parameters)
        {
            if (parameters == null || parameters.Length < 2)
                return null;

            var address = GetAddressParameter(parameters[0]);
            if (string.IsNullOrEmpty(address))
                return null;

            var position = GetStoragePosition(parameters[1]);
            if (position == null)
                return null;

            var blockParameter = GetBlockParameter(parameters, 2);
            if (!IsCurrentBlock(blockParameter))
                return null;

            var value = await _verifiedStateService.GetStorageAtAsync(address, position.Value)
                .ConfigureAwait(false);
            return value.ToHex(true);
        }

        private object HandleGetBlockNumber()
        {
            var header = _verifiedStateService.GetCurrentHeader();
            return new HexBigInteger(header.BlockNumber);
        }

        private static string GetAddressParameter(object param)
        {
            if (param is string s)
                return s;

            return param?.ToString();
        }

        private static BigInteger? GetStoragePosition(object param)
        {
            if (param is string s)
            {
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    return s.HexToBigInteger(false);
                }
                if (BigInteger.TryParse(s, out var result))
                {
                    return result;
                }
            }
            else if (param is BigInteger bi)
            {
                return bi;
            }
            else if (param is HexBigInteger hbi)
            {
                return hbi.Value;
            }
            else if (param is int i)
            {
                return new BigInteger(i);
            }
            else if (param is long l)
            {
                return new BigInteger(l);
            }

            return null;
        }

        private static BlockParameter GetBlockParameter(object[] parameters, int index)
        {
            if (parameters == null || parameters.Length <= index)
                return BlockParameter.CreateLatest();

            var param = parameters[index];

            if (param is BlockParameter bp)
                return bp;

            if (param is string s)
            {
                if (s == "latest" || s == "pending")
                    return BlockParameter.CreateLatest();
                if (s == "earliest")
                    return BlockParameter.CreateEarliest();
                if (s == "finalized")
                {
                    var finalizedParam = new BlockParameter();
                    finalizedParam.SetValue(BlockParameter.BlockParameterType.finalized);
                    return finalizedParam;
                }
                if (s == "safe")
                {
                    var safeParam = new BlockParameter();
                    safeParam.SetValue(BlockParameter.BlockParameterType.safe);
                    return safeParam;
                }
            }

            return BlockParameter.CreateLatest();
        }

        private bool IsCurrentBlock(BlockParameter blockParameter)
        {
            if (blockParameter == null)
                return true;

            var paramType = blockParameter.ParameterType;

            return paramType == BlockParameter.BlockParameterType.latest ||
                   paramType == BlockParameter.BlockParameterType.pending ||
                   paramType == BlockParameter.BlockParameterType.finalized ||
                   paramType == BlockParameter.BlockParameterType.safe;
        }

        private void OnFallbackTriggered(string method, Exception exception)
        {
            FallbackTriggered?.Invoke(this, new VerificationFallbackEventArgs(method, exception));
        }
    }

    public class VerificationFallbackEventArgs : EventArgs
    {
        public string Method { get; }
        public Exception Exception { get; }

        public VerificationFallbackEventArgs(string method, Exception exception)
        {
            Method = method;
            Exception = exception;
        }
    }
}
