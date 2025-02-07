using Nethereum.Blazor.Reown.AppKit.Wagmi;
using Nethereum.JsonRpc.Client;
using Nethereum.Reown.AppKit.Blazor.Wagmi;
using Nethereum.UI;
using Nethereum.Web3;
using System;
using System.Threading.Tasks;

namespace Nethereum.Reown.AppKit.Blazor;

internal class AppKitHostProvider : IEthereumHostProvider, IAppKit {
	public string Name { get; } = "Nethereum.AppKit";
	public bool Available { get; private set; }
	public string? SelectedAccount { get; private set; }
	public long SelectedNetworkChainId { get; private set; }
	public bool Enabled { get; private set; }
    public bool MultipleWalletsProvider => false; //False for now
    public bool MultipleWalletSelected { get; private set; } = false;

    public event Func<string?, Task>? SelectedAccountChanged;
	public event Func<long, Task>? NetworkChanged;
	public event Func<bool, Task>? AvailabilityChanged;
	public event Func<bool, Task>? EnabledChanged;

	private readonly IClient? client;
	private readonly AppKitInterceptor interceptor;
	private readonly Task initializationTask;

	public AppKitHostProvider(AppKitConfiguration configuration, IClient? client = null) {
		initializationTask = InitializeAsync(configuration);
		interceptor = new(this);
		this.client = client;
	}

	private async Task InitializeAsync(AppKitConfiguration configuration) {
		Available = true;
		_ = AvailabilityChanged?.Invoke(true);
		Enabled = true;
		_ = EnabledChanged?.Invoke(true);

		await AppKitInterop.InitializeAsync(configuration).ConfigureAwait(false);

		AppKitInterop.WatchAccount(AccountChanged);
		AppKitInterop.WatchChainId(ChainIdChanged);
	}

	public Task<IWeb3> GetWeb3Async() {
		IWeb3 web3 = client is null ? new Web3.Web3() : new Web3.Web3(client);
		web3.Client.OverridingRequestInterceptor = interceptor;
		return Task.FromResult(web3);
	}

	public async Task<string?> EnableProviderAsync() {
		GetAccountReturnType response = await AppKitInterop.EnableProviderAsync().ConfigureAwait(false);

		string? selectedAccount = response.IsConnected ? response.Address : null;
		Enabled = !string.IsNullOrEmpty(selectedAccount);

		bool accountChanged = !string.Equals(selectedAccount, SelectedAccount);

		SelectedAccount = selectedAccount;
		if (accountChanged && SelectedAccountChanged is not null) {
			await SelectedAccountChanged.Invoke(SelectedAccount).ConfigureAwait(false);
		}
		return SelectedAccount;
	}

	public async Task<bool> CheckProviderAvailabilityAsync() {
		await initializationTask;
		return true;
	}

	public async Task<string?> GetProviderSelectedAccountAsync() {
		if (string.IsNullOrWhiteSpace(SelectedAccount)) {
			GetAccountReturnType account;
			int retry = 10;
			while (true) {
				account = AppKitInterop.GetAccount();
				if (account.IsDisconnected) {
					SelectedAccount = null;
					break;
				}

				if (account.IsConnected
					|| !string.IsNullOrWhiteSpace(account.Address)
					|| retry <= 0) {
					SelectedAccount = account.Address;
					break;
				}
				retry--;
				await Task.Delay(1000).ConfigureAwait(false);
			}
		}

		return SelectedAccount;
	}

	public async Task<string> SignMessageAsync(string message) {
		return await AppKitInterop.SignMessageAsync(message).ConfigureAwait(false);
	}

	private void AccountChanged(GetAccountReturnType value) {
		SelectedAccount = value.Address;
		SelectedAccountChanged?.Invoke(SelectedAccount).ConfigureAwait(false);
	}

	private void ChainIdChanged(long chainId) {
		SelectedNetworkChainId = chainId;
		NetworkChanged?.Invoke(SelectedNetworkChainId).ConfigureAwait(false);
	}

	public void Open() {
		AppKitInterop.Open();
	}

	public void Close() {
		AppKitInterop.Close();
	}

	public void Disconnect() {
		AppKitInterop.Disconnect();
	}
}
