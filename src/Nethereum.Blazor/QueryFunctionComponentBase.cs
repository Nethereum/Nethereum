using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.UI;
using Nethereum.Web3;

namespace Nethereum.Blazor
{
    public abstract class QueryFunctionComponentBase<TFunctionMessage, TFunctionOutput> : ComponentBase
        where TFunctionMessage : FunctionMessage, new()
    {
        [Parameter] public string Title { get; set; } = typeof(TFunctionMessage).Name;
        [Parameter] public string ContractAddress { get; set; }
        [Parameter] public SelectedEthereumHostProviderService HostProvider { get; set; }
        [Parameter] public Type ServiceType { get; set; }
        [Parameter] public string ServiceMethodName { get; set; }
        [Parameter] public bool UseContractHandlerDirectly { get; set; } = false;

        [Parameter] public Func<object, TFunctionMessage, Task<TFunctionOutput>> ExecuteQuery { get; set; }
        [Parameter] public Func<Exception, object, string> HandleCustomError { get; set; }
        [Parameter] public TFunctionMessage FunctionInput { get; set; }

        protected TFunctionOutput Output;
        protected string ErrorMessage;
        protected bool IsLoading;
        protected bool HasQueried;

        protected override void OnInitialized()
        {
            FunctionInput ??= new TFunctionMessage();
        }

        protected override void OnParametersSet()
        {
            FunctionInput ??= new TFunctionMessage();
        }

        protected async Task QueryAsync()
        {
            ErrorMessage = null;
            Output = default;
            IsLoading = true;
            HasQueried = false;

            try
            {
                var web3 = await HostProvider.SelectedHost.GetWeb3Async();

                if (UseContractHandlerDirectly)
                {
                    var handler = web3.Eth.GetContractQueryHandler<TFunctionMessage>();
                    Output = await handler.QueryAsync<TFunctionOutput>(ContractAddress, FunctionInput);
                }
                else
                {
                    var service = CreateServiceInstance(web3);

                    if (ExecuteQuery != null)
                    {
                        Output = await ExecuteQuery(service, FunctionInput);
                    }
                    else
                    {
                        var methods = ServiceType?.GetMethods().Where(m => m.Name == ServiceMethodName).ToList();

                        var method = methods?.FirstOrDefault(m =>
                         {
                             var parameters = m.GetParameters();
                             return parameters.Length == 2 &&
                                    parameters[0].ParameterType.IsAssignableFrom(typeof(TFunctionMessage)) &&
                                    parameters[1].ParameterType == typeof(BlockParameter);
                         })
                         ?? methods?.FirstOrDefault(m =>
                         {
                             var parameters = m.GetParameters();
                             return parameters.Length == 1 &&
                                    parameters[0].ParameterType.IsAssignableFrom(typeof(TFunctionMessage));
                         });

                        if (method == null)
                            throw new InvalidOperationException($"No suitable method '{ServiceMethodName}' found on '{ServiceType?.Name}'");

                        var args = method.GetParameters().Length == 2
                            ? new object[] { FunctionInput, null }
                            : new object[] { FunctionInput };

                        var resultTask = method.Invoke(service, args) as Task<TFunctionOutput>;
                        Output = await resultTask;
                    }
                }

                HasQueried = true;
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

            return Activator.CreateInstance(ServiceType, web3, ContractAddress);
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

        protected static readonly HashSet<string> DefaultExcludedQueryProps = new()
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
            nameof(FunctionMessage.AuthorisationList)
        };
    }
}
