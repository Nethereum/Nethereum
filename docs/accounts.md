
## Accounts in Web3 Nethereum

Every transaction in Ethereum needs to be sent and signed by an account. The account needs to verify (sign) that is the account holder of their Ether or the one that intents to interact with a smart contract.

To send a transaction you will either mananage your account and sign the raw transaction locally, or the account will be managed by the client (Parity / Geth), requiring to send the password at the time of sending a transaction or unlock the account before hand.
 
In Nethereum.Web3, to simplify and abstract the process, there are two types of account that you can use. An "Account" object or a "ManagedAccount" object. Both store the account information required to send a transaction, private key, or password.

At the time of sending a transaction if using the TransactionManager, deploying a contract or using a contract function, the right method to deliver the transaction will be chosen, either the transaction will be signed offline using the private key or a personal_sendTransaction message will be sent using the password.

### Working with an Account

An account is just created with a private key, you can generate a new private key and store it using the Web3 storage definition (compatible with all clients), or load an exiting one from any storage, or from the key storage folder of your locally installed client.

One of the major advantages, apart from security (avoiding transfer in plain text the password to process the transaction), is not needing to have a local installation of a client, allowing you to target public nodes like Infura.

#### Loading an existing Account

Encrypted Accounts key store files can be found in different locations depending on the client and operating system:

Geth:

* Linux: ~/.ethereum/keystore
* Mac: /Library/Ethereum/keystore
* Windows: %APPDATA%/Ethereum

Parity:

* Windows %APPDATA%\Roaming\Parity\Ethereum
* Mac: /Library/Application Support/io.parity.ethereum
* Linux: ~/.local/share/io.parity.ethereum

When using net451 or above you can load directly your file:

```csharp
var password = "password";
var accountFilePath = @"c:\xxx\UTC--2015-11-25T05-05-03.116905600Z--12890d2cce102216644c59dae5baed380d84830c";
var account = Account.LoadFromKeyStoreFile(accountFilePath, string password);
```

If you are targetting other framework like core, netstandard, portable loading directly from a file is not supported, to allow for major platform compatibility, in this scenario you will need to extract the json fist and pass it as a parameter.

```csharp
 var password = "password";
 var keyStoreEncryptedJson =
             @"{""crypto"":{""cipher"":""aes-128-ctr"",""ciphertext"":""b4f42e48903879b16239cd5508bc5278e5d3e02307deccbec25b3f5638b85f91"",""cipherparams"":{""iv"":""dc3f37d304047997aa4ef85f044feb45""},""kdf"":""scrypt"",""mac"":""ada930e08702b89c852759bac80533bd71fc4c1ef502291e802232b74bd0081a"",""kdfparams"":{""n"":65536,""r"":1,""p"":8,""dklen"":32,""salt"":""2c39648840b3a59903352b20386f8c41d5146ab88627eaed7c0f2cc8d5d95bd4""}},""id"":""19883438-6d67-4ab8-84b9-76a846ce544b"",""address"":""12890d2cce102216644c59dae5baed380d84830c"",""version"":3}";
var account = Nethereum.Web3.Accounts.Account.LoadFromKeyStore(keyStoreEncryptedJson, password);
```

#### Working with an Account in Web3

Once you have loaded your private keys into your account, if Web3 is instantiated with that acccount all the transactions made using the TransactionManager, Contract deployment or Functions will signed offline using the latest nonce key.

For example, in this scenario we are creating an account with the private key from a keystore file, and creating a new instance of Web3 using the default "http://localhost:8545".

```csharp
var password = "password";
var accountFilePath = @"c:\xxx\UTC--2015-11-25T05-05-03.116905600Z--12890d2cce102216644c59dae5baed380d84830c";
var account = Nethereum.Web3.Accounts.Account.LoadFromKeyStoreFile(accountFilePath, string password);

var web3 = new Nethereum.Web3.Web3(account);
```

Now all these type of transactions will be signed offline 
Transfer an amount to another address, using the transaction manager
 
```csharp
await web3.TransactionManager.SendTransactionAsync(account.Address, addressTo, new HexBigInteger(20));

```

Deploy a contract

```csharp
 web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000),
                            multiplier)
```

Make a contract Function transaction

```csharp
var multiplyFunction = contract.GetFunction("multiply");
await multiplyFunction.SendTransactionAsync(senderAddress,7);
```

#### Creating a new Account

To create a new account you just need to generate a new private key, Nethereum.Signer provides a method to do this using SecureRandom. The Account object accepts just the private key as a constructor, to reduce any coupling with private key generation, and prescriptive way to generate private keys.

```csharp
var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
var privateKey = ecKey.GetPrivateKeyAsBytes().ToHex();
var account = new Nethereum.Accounts.Account(privateKey);
```

The Nethereum.KeyStore library, allows you to encrypt and save your private key, in a compatible way to all the clients.

### Working with a Managed Account in Web3

Clients retrieve the private key for an account (if stored on their keystore folder) using a password provided to decrypt the file. This is done when unlocking an account, or just at the time of sending a transaction if using personal_sendTransaction with a password.

Having an account unlocked for a certain period of time might be a security issue, so the prefered option in this scenario, is to use the rpc method personal_sendTransaction.

Nethereum.Web3 wraps this functionality by using a ManagedAccount, having the managed account storing the account address and the password information.

```csharp
var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
var password = "password";

var account = new ManagedAccount(senderAddress, password);
var web3 = new Web3.Web3(account);
```

When used in conjuction with Web3, now in the same way to an "Account", you can:

Transfer an amount to another address, using the transaction manager
 
```csharp
await web3.TransactionManager.SendTransactionAsync(account.Address, addressTo, new HexBigInteger(20));

```

Deploy a contract

```csharp
 web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, new HexBigInteger(900000),
                            multiplier)
```

Make a contract Function transaction

```csharp
var multiplyFunction = contract.GetFunction("multiply");
await multiplyFunction.SendTransactionAsync(senderAddress,7);
```
