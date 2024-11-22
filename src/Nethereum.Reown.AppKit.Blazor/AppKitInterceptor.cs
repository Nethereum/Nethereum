using Nethereum.Blazor.Reown.AppKit.Wagmi;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Reown.AppKit.Blazor.Wagmi;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.HostWallet;
using Nethereum.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Reown.AppKit.Blazor;

internal class AppKitInterceptor : RequestInterceptor {
	private class AddSwitchChainParameter {
		public BigInteger ChainId { get; init; }
		public required AddEthereumChainParameter AddEthereumChainParameter { get; init; }
	}

	private static readonly JsonSerializerSettings settings = new() {
		ContractResolver = new CamelCasePropertyNamesContractResolver(),
		NullValueHandling = NullValueHandling.Ignore,
		MissingMemberHandling = MissingMemberHandling.Ignore,
	};

	private readonly IEthereumHostProvider ethereumHostProvider;

	public AppKitInterceptor(IEthereumHostProvider ethereumHostProvider) {
		this.ethereumHostProvider = ethereumHostProvider;
	}

	private async Task<T> SendRequestAsync<T>(Func<Task<T>> interceptedSendRequestAsync, int id, string method, string? route, params object[] paramList) {
		string wagmiMethod;
		string? wagmiParameter = null;
		switch (method) {
			case "eth_estimateGas": {
				wagmiMethod = WagmiMethods.EstimateGas;
				CallInput transaction = (CallInput)paramList[0];
				transaction.From ??= ethereumHostProvider.SelectedAccount;
				wagmiParameter = JsonConvert.SerializeObject(transaction, settings);
			}
			break;

			case "eth_call": {
				wagmiMethod = WagmiMethods.Call;
				CallInput transaction = (CallInput)paramList[0];
				transaction.From ??= ethereumHostProvider.SelectedAccount;
				wagmiParameter = JsonConvert.SerializeObject(transaction, settings);
			}
			break;

			case "eth_sendTransaction": {
				wagmiMethod = WagmiMethods.SendTransaction;
				TransactionInput transaction = (TransactionInput)paramList[0];
				transaction.From ??= ethereumHostProvider.SelectedAccount;
				wagmiParameter = JsonConvert.SerializeObject(transaction, settings);
			}
			break;

			case "eth_chainId": {
				wagmiMethod = WagmiMethods.GetChainId;
			}
			break;

			case "eth_getTransactionReceipt": {
				wagmiMethod = WagmiMethods.GetTransactionReceipt;
				wagmiParameter = $$"""{ "hash": "{{paramList[0]}}" }""";
			}
			break;

			case "eth_signTypedData_v4": {
				wagmiMethod = WagmiMethods.SignTypedData;
				wagmiParameter = (string)paramList[0];
			}
			break;

			case "eth_getBalance": {
				wagmiMethod = WagmiMethods.GetBalance;
				wagmiParameter = $$"""{ "address": "{{paramList[0]}}" }""";
			}
			break;

			case "wallet_addEthereumChain": {
				wagmiMethod = WagmiMethods.SwitchChain;
				AddEthereumChainParameter parameter = (AddEthereumChainParameter)paramList[0];
				AddSwitchChainParameter addParamter = new() {
					ChainId = parameter.ChainId.Value,
					AddEthereumChainParameter = parameter
				};
				wagmiParameter = JsonConvert.SerializeObject(addParamter, settings);
			}
			break;
			case "wallet_switchEthereumChain": {
				wagmiMethod = WagmiMethods.SwitchChain;
				SwitchEthereumChainParameter parameter = (SwitchEthereumChainParameter)paramList[0];
				wagmiParameter = $$"""{ "chainId": {{parameter.ChainId.Value}} }""";
			}
			break;
			case "personal_sign": {
				wagmiMethod = WagmiMethods.SignMessage;
				wagmiParameter = $$"""{ "message": { "raw": "{{paramList[0]}}" } }""";
			}
			break;

			default: {
				Console.WriteLine($"Default Intercept request: {method}");
				return await interceptedSendRequestAsync().ConfigureAwait(false);
			}
		}


		string responseJson = await AppKitInterop.SendTransactionAsync(id, wagmiMethod, wagmiParameter).ConfigureAwait(false);
		RpcResponseMessage response = JsonConvert.DeserializeObject<RpcResponseMessage>(responseJson)!;
		return ConvertResponse<T>(response, route);
	}

	public override async Task<object> InterceptSendRequestAsync<T>(Func<RpcRequest, string?, Task<T>> interceptedSendRequestAsync, RpcRequest request, string? route = null) {
		return await SendRequestAsync(
			async () => await interceptedSendRequestAsync(request, route).ConfigureAwait(false),
			(int)request.Id,
			request.Method,
			route,
			request.RawParameters).ConfigureAwait(false);
	}

	public override async Task<object> InterceptSendRequestAsync<T>(Func<string, string?, object[], Task<T>> interceptedSendRequestAsync, string method, string? route = null, params object[] paramList) {
		return await SendRequestAsync(
			async () => await interceptedSendRequestAsync(method, route, paramList).ConfigureAwait(false),
			0,
			method,
			route,
			paramList).ConfigureAwait(false);
	}

	private T ConvertResponse<T>(RpcResponseMessage response, string? route) {
		HandleRpcError(response);
		try {
			return response.GetResult<T>();
		} catch (FormatException formatException) {
			throw new RpcResponseFormatException("Invalid format found in RPC response", formatException);
		}
	}

	protected void HandleRpcError(RpcResponseMessage response) {
		if (!response.HasError) {
			return;
		}

		throw new RpcResponseException(new(response.Error.Code, response.Error.Message, response.Error.Data));
	}
}