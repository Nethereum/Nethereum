using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.UI;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.Blazor
{
    public abstract class ContractDeploymentComponentBase<TDeploymentMessage> : ComponentBase
        where TDeploymentMessage : ContractDeploymentMessage, new()
    {
        [Parameter] public string Title { get; set; } = typeof(TDeploymentMessage).Name;
        [Parameter] public SelectedEthereumHostProviderService HostProvider { get; set; }
        [Parameter] public TDeploymentMessage DeploymentMessage { get; set; } = new();
        [Parameter] public string ContractAddress { get; set; }
        [Parameter] public EventCallback<string> ContractAddressChanged { get; set; }
        [Parameter] public Type ServiceType { get; set; }
        [Parameter] public IEnumerable<Type> AdditionalEventTypes { get; set; }

        protected TransactionReceipt Receipt;
        protected string ErrorMessage;
        protected bool IsLoading;
        protected ContractServiceBase contractService;

        protected static string FormatGwei(BigInteger? weiValue)
        {
            if (!weiValue.HasValue) return "";
            return UnitConversion.Convert.FromWei(weiValue.Value, UnitConversion.EthUnit.Gwei).ToString();
        }

        protected async Task DeployAsync()
        {
            ErrorMessage = null;
            Receipt = null;
            IsLoading = true;

            try
            {
                var web3 = await HostProvider.SelectedHost.GetWeb3Async();
                var handler = web3.Eth.GetContractDeploymentHandler<TDeploymentMessage>();
                Receipt = await handler.SendRequestAndWaitForReceiptAsync(DeploymentMessage);
                ContractAddress = Receipt.ContractAddress;
                await ContractAddressChanged.InvokeAsync(ContractAddress);

                if (ServiceType != null && !string.IsNullOrEmpty(ContractAddress))
                {
                    var serviceInstance = Activator.CreateInstance(ServiceType, web3, ContractAddress);
                    contractService = serviceInstance as ContractServiceBase;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.ToString();
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected static readonly HashSet<string> DefaultExcludedDeploymentProps = new()
        {
            nameof(ContractDeploymentMessage.FromAddress),
            nameof(ContractDeploymentMessage.AmountToSend),
            nameof(ContractDeploymentMessage.Gas),
            nameof(ContractDeploymentMessage.GasPrice),
            nameof(ContractDeploymentMessage.Nonce),
            nameof(ContractDeploymentMessage.MaxFeePerGas),
            nameof(ContractDeploymentMessage.MaxPriorityFeePerGas),
            nameof(ContractDeploymentMessage.TransactionType),
            nameof(ContractDeploymentMessage.AccessList),
            nameof(FunctionMessage.AuthorisationList)
        };
    }
}
