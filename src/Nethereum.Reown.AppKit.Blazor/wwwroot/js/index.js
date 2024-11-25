import { createAppKit, WagmiAdapter, WagmiCore } from 'https://cdn.jsdelivr.net/npm/@reown/appkit-cdn@1.4.1/dist/appkit.min.js'

var appKit;
var wagmiAdapter;
var modalConnectCallback;
var initialized = false;
var debug = false;

async function InitializeAsync(configParametersJson) {
	if (initialized) {
		console.error("AppKit is not initialized. Call InitializeAsync first.");
		return;
	}
	initialized = true;

	const configParameters = JSON.parse(configParametersJson);
	debug = configParameters?.debug ?? false;
	log(`Configuration: ${configParameters}`);

	const metadata = {
		name: configParameters.name,
		description: configParameters.description,
		url: configParameters.url,
		icons: configParameters.icons
	};
	const projectId = configParameters.projectId;
	const networkList = configParameters.networks;
	const themeVariables = configParameters.themeVariables == null ? undefined : {
		"--w3m-font-family": configParameters.themeVariables.w3mFontFamily,
		"--w3m-accent": configParameters.themeVariables.w3mAccent,
		"--w3m-color-mix": configParameters.themeVariables.w3mColorMix,
		"--w3m-color-mix-strength": configParameters.themeVariables.w3mColorMixStrength,
		"--w3m-font-size-master": configParameters.themeVariables.w3mFontSizeMaster,
		"--w3m-border-radius-master": configParameters.themeVariables.w3mBorderRadiusMaster,
		"--w3m-z-index": configParameters.themeVariables.w3mZIndex,
	};

	wagmiAdapter = new WagmiAdapter({
		projectId,
		networks: networkList,
	});

	WagmiCore.reconnect(wagmiAdapter.wagmiConfig);

	const config = {
		adapters: [wagmiAdapter],
		networks: networkList,
		defaultNetwork: configParameters.defaultNetwork,

		themeMode: configParameters.themeMode,
		coinbasePreference: configParameters.coinbasePreference,
		themeVariables: themeVariables,
		projectId: projectId,
		metadata: metadata,

		allWallets: configParameters.allWallets,
		featuredWalletIds: configParameters.featuredWalletIds,
		includeWalletIds: configParameters.includeWalletIds,
		excludeWalletIds: configParameters.excludeWalletIds,
		termsConditionsUrl: configParameters.termsConditionsUrl,
		privacyPolicyUrl: configParameters.privacyPolicyUrl,
		disableAppend: configParameters.disableAppend,
		enableWallets: configParameters.enableWallets,
		enableEIP6963: configParameters.enableEIP6963,
		enableCoinbase: configParameters.enableCoinbase,
		enableInjected: configParameters.enableInjected,
		debug: configParameters.debug,

		features: {
			swaps: configParameters.swaps,
			onramp: configParameters.onramp,
			email: configParameters.email,
			emailShowWallets: configParameters.emailShowWallets,
			socials: configParameters.socials,
			history: configParameters.history,
			analytics: configParameters.analytics,
			legalCheckbox: configParameters.legalCheckbox,
		},
	};


	appKit = createAppKit(config);

	appKit.subscribeEvents((event) => {
		log(`${event.data.event} ${JSON.stringify(event.data)}`);
		if (modalConnectCallback === undefined
			|| event.data.event !== "MODAL_CLOSE") {
			return;
		}

		log(`${event.data.event} ${JSON.stringify(event.data.properties)}`);
		modalConnectCallback(event.data.properties.connected);
		modalConnectCallback = undefined;
	});
}

function Open() {
	log('Open');
	if (!ValidateInitialized()) { return; }

	appKit.open();
}

function Close() {
	log('Close');
	if (!ValidateInitialized()) { return; }

	appKit.close();
}

function Disconnect() {
	log('Disconnect');
	if (!ValidateInitialized()) { return; }

	appKit.connectionControllerClient?.disconnect();
}

function WatchAccount(callback) {
	log('WatchAccount');
	if (!ValidateInitialized()) { return; }

	WagmiCore.watchAccount(wagmiAdapter.wagmiConfig, {
		onChange(data) {
			callback(SerializeJson(data));
		}
	});
}

function WatchChainId(callback) {
	log('WatchChainId');
	if (!ValidateInitialized()) { return; }

	callback(WagmiCore.getChainId(wagmiAdapter.wagmiConfig));

	WagmiCore.watchChainId(wagmiAdapter.wagmiConfig, {
		onChange(data) {
			callback(data);
		}
	});
}

async function EnableProviderAsync() {
	log('EnableProviderAsync');
	if (!ValidateInitialized()) { return; }

	let open = !appKit.getIsConnectedState();
	log(`EnableProviderAsync ${open}`);
	let account;
	if (!open) {
		account = WagmiCore.getAccount(wagmiAdapter.wagmiConfig);
		log(`EnableProviderAsync ${account}`);
		if (account.isConnecting) {
			account = await WaitConnectedAsync();
		}
		open = account.isDisconnected || account.isConnecting;
	}
	if (open) {
		let promise = new Promise(resolve => {
			modalConnectCallback = result => resolve(result);
		});

		await appKit.open();

		if (await promise) {
			account = await WaitConnectedAsync();
		}
	}
	return SerializeJson(account);
}

async function WaitConnectedAsync() {
	let account;
	for (let i = 0; i < 10; i++) {
		account = WagmiCore.getAccount(wagmiAdapter.wagmiConfig);
		if (account.isConnected) { break; }
		await new Promise(resolve => setTimeout(resolve, 300));
	}
	return account;
}

function GetAccount() {
	log('GetAccount');
	if (!ValidateInitialized()) { return; }
	return SerializeJson(WagmiCore.getAccount(wagmiAdapter.wagmiConfig));
}

async function SignMessageAsync(message) {
	if (!ValidateInitialized()) { return; }

	return await WagmiCore.signMessage(wagmiAdapter.wagmiConfig, { message: message });
}

async function SendTransactionAsync(id, methodName, parameter) {
	return SerializeJson(await ExecuteCallAsync(id, methodName, parameter));
}

async function ExecuteCallAsync(id, methodName, parameter) {
	if (!initialized) {
		console.error("AppKit is not initialized. Call Initialize first.");
		return { id, error: "AppKit is not initialized. Call Initialize first." };
	}

	let parameterObj = parameter === "" ? undefined : JSON.parse(parameter);
	let result;
	try {
		log(`Excute ${methodName} parameters: ${parameter}`);
		result = await WagmiCore[methodName](wagmiAdapter.wagmiConfig, parameterObj)
		log(`Excute ${methodName} Result: ${result}`);
	} catch (error) {
		if (error.name === 'TransactionReceiptNotFoundError') {
			return { jsonrpc: "2.0", id, result: undefined, error: undefined };
		}
		console.error("[AppKit] Error executing call", error);
		let errorJson = JSON.stringify(error);
		return { jsonrpc: "2.0", id, result: undefined, error: { message: error.message, data: errorJson } };
	}

	if (result === undefined || result === null) {
		return { jsonrpc: "2.0", id, result: undefined, error: undefined };
	}

	let resultJson;
	switch (methodName) {
		case 'getChainId':
		case 'estimateGas':
			resultJson = IntToHex(result);
			break;
		case 'getBalance':
			resultJson = IntToHex(result.value);
			break;
		case 'sendTransaction':
		case 'signTypedData':
			resultJson = result;
			break;
		case 'call':
			resultJson = result.data;
			break;
		case 'getTransactionReceipt':
			result.transactionIndex = IntToHex(result.transactionIndex);
			result.blockNumber = IntToHex(result.blockNumber);
			result.cumulativeGasUsed = IntToHex(result.cumulativeGasUsed);
			result.gasUsed = IntToHex(result.gasUsed);
			result.effectiveGasPrice = IntToHex(result.effectiveGasPrice);
			result.status = ConvertStatus(result.status);
			result.type = ConvertType(result.type);
			resultJson = result;
			break;
		default:
			resultJson = SerializeJson(result);
			break;
	}

	// Call the callback with the result
	return { jsonrpc: "2.0", id, result: resultJson, error: undefined };
}

function IntToHex(value) {
	if (value === null || value === undefined) {
		return null;
	}
	return `0x${value.toString(16)}`;
}

function ConvertStatus(value) {
	if (value === 'success') {
		return '0x1';
	}
	return '0x0';
}

function ConvertType(value) {
	switch (value) {
		case "eip1559":
			return '0x2';
		case "eip2930":
			return '0x1';
		default:
			return `0x0`;
	}
}

function SerializeJson(obj) {
	let cache = [];
	let resultJson = JSON.stringify(obj, (_, value) => {
		// Handle circular references
		if (typeof value === 'object' && value !== null) {
			if (cache.indexOf(value) >= 0) return;
			cache.push(value);
		}
		// Check if the value is a BigInt and convert it to a string
		if (typeof value === 'bigint') {
			return value.toString();
		}
		return value;
	});
	return resultJson;
}

function ValidateInitialized() {
	if (initialized) {
		return true;
	}

	console.error("AppKit is not initialized. Call InitializeAsync first.");
	return false;
}

function log(message) {
	if (!debug) {
		return;
	}
	console.log(message);
}

export { InitializeAsync, EnableProviderAsync, Open, Close, Disconnect, WatchAccount, WatchChainId, GetAccount, SignMessageAsync, SendTransactionAsync };
