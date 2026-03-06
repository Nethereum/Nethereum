using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.UI;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.Blazor
{
    public abstract class TransactionFunctionComponentBase<TFunctionMessage> : ComponentBase
        where TFunctionMessage : FunctionMessage, new()
    {
        [Parameter] public string Title { get; set; } = typeof(TFunctionMessage).Name;
        [Parameter] public string ContractAddress { get; set; }
        [Parameter] public SelectedEthereumHostProviderService HostProvider { get; set; }

        [Parameter] public Type ServiceType { get; set; }
        [Parameter] public string ServiceRequestMethodName { get; set; }
        [Parameter] public string ServiceRequestAndWaitForReceiptMethodName { get; set; }
        [Parameter] public bool UseContractHandlerDirectly { get; set; }

        [Parameter] public Func<object, TFunctionMessage, Task<string>> ExecuteSend { get; set; }
        [Parameter] public Func<object, TFunctionMessage, Task<TransactionReceipt>> ExecuteSendAndWait { get; set; }
        [Parameter] public Func<Exception, object, string> HandleCustomError { get; set; }
        [Parameter] public IEnumerable<Type> AdditionalEventTypes { get; set; }

        protected TFunctionMessage FunctionInput = new();
        protected string TransactionHash;
        protected TransactionReceipt Receipt;
        protected string ErrorMessage;
        protected bool IsLoading;
        protected ContractServiceBase contractService;

        protected static string FormatGwei(BigInteger? weiValue)
        {
            if (!weiValue.HasValue) return "";
            return UnitConversion.Convert.FromWei(weiValue.Value, UnitConversion.EthUnit.Gwei).ToString();
        }

        protected async Task SendTransactionAsync()
        {
            await ExecuteTransactionCore(async (svc, input) =>
            {
                if (ExecuteSend != null)
                    return await ExecuteSend(svc, input);

                if (UseContractHandlerDirectly)
                {
                    var web3 = await HostProvider.SelectedHost.GetWeb3Async();
                    var handler = web3.Eth.GetContractTransactionHandler<TFunctionMessage>();
                    return await handler.SendRequestAsync(ContractAddress, input);
                }

                var methods = ServiceType?.GetMethods().Where(m => m.Name == ServiceRequestMethodName).ToList();

                var method = methods?.FirstOrDefault(m =>
                {
                    var parameters = m.GetParameters();
                    return parameters.Length == 1 &&
                           parameters[0].ParameterType.IsAssignableFrom(typeof(TFunctionMessage));
                });

                if (method == null)
                    throw new InvalidOperationException($"No suitable method '{ServiceRequestMethodName}' found on '{ServiceType?.Name}'");

                var resultTask = method.Invoke(svc, new object[] { input }) as Task<string>;
                return await resultTask;
            });
        }

        protected async Task SendTransactionAndWaitAsync()
        {
            await ExecuteTransactionCore(async (svc, input) =>
            {
                if (ExecuteSendAndWait != null)
                {
                    Receipt = await ExecuteSendAndWait(svc, input);
                    return Receipt.TransactionHash;
                }

                if (UseContractHandlerDirectly)
                {
                    var web3 = await HostProvider.SelectedHost.GetWeb3Async();
                    var handler = web3.Eth.GetContractTransactionHandler<TFunctionMessage>();
                    Receipt = await handler.SendRequestAndWaitForReceiptAsync(ContractAddress, input);
                    return Receipt.TransactionHash;
                }

                var methods = ServiceType?.GetMethods().Where(m => m.Name == ServiceRequestAndWaitForReceiptMethodName).ToList();

                var method = methods?.FirstOrDefault(m =>
                {
                    var parameters = m.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType.IsAssignableFrom(typeof(TFunctionMessage)) &&
                           parameters[1].ParameterType == typeof(CancellationTokenSource);
                });

                if (method == null)
                    throw new InvalidOperationException($"No suitable method '{ServiceRequestAndWaitForReceiptMethodName}' found on '{ServiceType?.Name}'");

                var resultTask = method.Invoke(svc, new object[] { input, null }) as Task<TransactionReceipt>;
                Receipt = await resultTask;
                return Receipt.TransactionHash;
            });
        }

        private async Task ExecuteTransactionCore(Func<object, TFunctionMessage, Task<string>> transactionFunc)
        {
            ErrorMessage = null;
            TransactionHash = null;
            Receipt = null;
            IsLoading = true;

            try
            {
                var web3 = await HostProvider.SelectedHost.GetWeb3Async();
                var service = CreateServiceInstance(web3);
                TransactionHash = await transactionFunc(service, FunctionInput);
            }
            catch (Exception ex)
            {
                ErrorMessage = await ResolveErrorMessageAsync(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected object CreateServiceInstance(IWeb3 web3)
        {
            if (ServiceType == null)
                throw new InvalidOperationException("ServiceType must be provided");

            var service = Activator.CreateInstance(ServiceType, web3, ContractAddress);
            contractService = service as ContractServiceBase;
            return service;
        }

        protected async Task<string> ResolveErrorMessageAsync(Exception ex)
        {
            if (HandleCustomError != null && ServiceType != null)
            {
                var web3 = await HostProvider.SelectedHost.GetWeb3Async();
                var svc = CreateServiceInstance(web3);
                return HandleCustomError(ex, svc);
            }

            if (ex is SmartContractCustomErrorRevertException revert && ServiceType != null)
            {
                try
                {
                    var web3 = await HostProvider.SelectedHost.GetWeb3Async();
                    var svc = CreateServiceInstance(web3) as ContractWeb3ServiceBase;
                    var decoded = svc?.FindCustomErrorException(revert);
                    return decoded?.ToString() ?? revert.Message;
                }
                catch
                {
                    return revert.Message;
                }
            }

            return ex.ToString();
        }

        protected static readonly HashSet<string> DefaultExcludedTransactionProps = new()
        {
            nameof(FunctionMessage.FromAddress),
            nameof(FunctionMessage.AmountToSend),
            nameof(FunctionMessage.Gas),
            nameof(FunctionMessage.GasPrice),
            nameof(FunctionMessage.Nonce),
            nameof(FunctionMessage.MaxFeePerGas),
            nameof(FunctionMessage.MaxPriorityFeePerGas),
            nameof(FunctionMessage.TransactionType),
            nameof(FunctionMessage.AccessList),
            nameof(FunctionMessage.AuthorisationList),
        };
    }
}
