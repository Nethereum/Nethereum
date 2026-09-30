# Nethereum.Quorum

Nethereum Quorum is the Nethereum extension to interact with [Quorum](https://github.com/jpmorganchase/quorum), the permissioned implementation of Ethereum supporting data privacy created by JP Morgan.

The cross-platform library supports .Net Core, Mono, Linux, iOS, Android, Raspberry PI, Xbox and of course Windows.

## Issues, Requests and help

Please join the chat at:  [![Join the chat at https://gitter.im/juanfranblanco/Ethereum.RPC](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/juanfranblanco/Ethereum.RPC?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

We should be able to answer there any simple queries, general comments or requests, everyone is welcome. In a similar feel free to raise any issue or pull request.

## Quick installation

| Package       | Nuget         | 
| ------------- |:-------------:|
| Nethereum.Quorum| [![NuGet version](https://badge.fury.io/nu/nethereum.quorum.svg)](https://badge.fury.io/nu/nethereum.quorum)|

## Usage

Public interaction with Quorum is the same as interacting with Geth, if you require more information on this please refer to the general Netherum documentation in [Read the docs](https://nethereum.readthedocs.io/en/latest/).

For private interaction or to access the Quorum RPC methods, a specialised Web3, Web3Quorum is required.

PrivateFor will be set as following:

```csharp
var web3Node1 = new Web3Quorum(urlNode1);
var privateFor = new List<string>(new[] { "ROAZBWtSacxXQrOe3FGAqJDyJjFePR5ce4TSIzmJ0Bc=" });
web3Node1.SetPrivateRequestParameters(privateFor);
```
afterwards all the transactions will use the PrivateFor parameter.

```csharp
using Nethereum.RPC.TransactionReceipts;

var contract = web3Node1.Eth.GetContract(abi, address);
var functionSet = contract.GetFunction("set");
var account = await web3Node1.Eth.CoinBase.SendRequestAsync();

// TransactionReceiptPollingService only exposes SendRequestAndWaitForReceiptAsync (there is no SendRequestAsync)
var transactionService = new TransactionReceiptPollingService(web3Node1.TransactionManager);
var receipt = await transactionService.SendRequestAndWaitForReceiptAsync(() => functionSet.SendTransactionAsync(account, 4));
```

For a full example of usage using the [7nodes sample of Quorum](https://github.com/jpmorganchase/quorum-examples/tree/master/examples/7nodes) check the [Unit Test](https://github.com/Nethereum/Nethereum/blob/master/tests/Nethereum.Quorum.Tests/QuorumPrivateContractTests.cs)

### Quorum RPC 

The Quorum RPC methods can be accessed using web3.Quorum to retrieve the CanonicalHash, BlockMaker, Voter, MakeBlock.. 
Here are some examples from the Unit tests using the [7nodes sample of Quorum](https://github.com/jpmorganchase/quorum-examples/tree/master/examples/7nodes)

```csharp
 [Fact]
  public async void ShouldReturnCanonicalHashForBlockNumber1()
  {
      var web3 = new Web3Quorum(DefaultSettings.GetDefaultUrl());
      var canonicalHash = await web3.Quorum.CanonicalHash.SendRequestAsync(1);
      Assert.Equal("0x8bb911238205c6d5e9841335c9c5aff3dfae4c0f6b0df28100737c2660a15f8d", canonicalHash);
  }

  [Fact]
  public async void ShouldReturnTrueWhenCallingIsBlockMakerForCoinbase()
  {
      var web3 = new Web3Quorum(DefaultSettings.GetDefaultUrl());
      var isBlockMaker = await web3.Quorum.IsBlockMaker.SendRequestAsync(await web3.Eth.CoinBase.SendRequestAsync());
      Assert.Equal(true, isBlockMaker);
  }

  [Fact]
  public async void ShouldReturnTrueWhenCallingIsVoterForCoinbase()
  {
      var web3 = new Web3Quorum(DefaultSettings.GetDefaultUrl());
      var isVoter = await web3.Quorum.IsVoter.SendRequestAsync(await web3.Eth.CoinBase.SendRequestAsync());
      Assert.Equal(true, isVoter);
  }

  [Fact]
  public async void ShouldBeAbleToMakeBlocksBypassingBlockStrategy()
  {
      //Node2 is a Block Maker
      //This might fail.. depending on the canonicalhash.. (quorum)            
      var web3 = new Web3Quorum(DefaultSettings.QuorumIPAddress + ":22001");
      var hash = await web3.Quorum.MakeBlock.SendRequestAsync();
      Assert.NotNull(hash);
  }

  [Fact]
  public async void ShouldBeAbleToVote()
  {
      //Node 5 can vote
      var web3 = new Web3Quorum(DefaultSettings.QuorumIPAddress + ":22004");
      var block =
          await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());

      var hash = await web3.Quorum.Vote.SendRequestAsync(block.BlockHash);
      Assert.NotNull(hash);
  }

  [Fact]
  public async void ShouldPauseResumeBlockMaker()
  {
      //Node 2 is a BlockMaker
      var web3 = new Web3Quorum(DefaultSettings.QuorumIPAddress + ":22001");
      await web3.Quorum.PauseBlockMaker.SendRequestAsync();
      await web3.Quorum.ResumeBlockMaker.SendRequestAsync();
  }

  [Fact]
  public async void ShouldGetNodeInfoOfBlockMaker()
  {
      var web3 = new Web3Quorum(DefaultSettings.QuorumIPAddress + ":22001");
      var nodeInfo = await web3.Quorum.NodeInfo.SendRequestAsync();
      Assert.Equal("active", nodeInfo.BlockMakeStratregy.Status);
  } 
```

### Permission, Privacy, Raft, IBFT, ContractExtensions, DebugQuorum

`Web3Quorum` also exposes `Permission`, `Privacy`, `Raft`, `IBFT`, `ContractExtensions` and `DebugQuorum`
(`src/Nethereum.Quorum/Web3Quorum.cs:67-74`), alongside `Quorum`.

**Permission** (node/account/org permissioning, `src/Nethereum.Quorum/RPC/Services/IPermissionService.cs`):

```csharp
var allowed = await web3.Permission.ConnectionAllowed.SendRequestAsync(
    "enode://pubkey@ip:port", "10.0.0.1", 21000);
```

Also: `AcctList`, `AddAccountToOrg`, `AddNewRole`, `AddNode`, `AddOrg`, `AddSubOrg`, `ApproveAdminRole`,
`ApproveBlackListedAccountRecovery`, `ApproveBlackListedNodeRecovery`, `ApproveOrg`, `ApproveOrgStatus`,
`AssignAdminRole`, `ChangeAccountRole`, `GetOrgDetails`, `NodeList`, `OrgList`, `RecoverBlackListedAccount`,
`RecoverBlackListedNode`, `RemoveRole`, `RoleList`, `TransactionAllowed`, `UpdateAccountStatus`,
`UpdateNodeStatus`, `UpdateOrgStatus`.

**Privacy** (Quorum's headline private-transaction feature, `src/Nethereum.Quorum/RPC/Services/IPrivacyService.cs`):

```csharp
var privateReceipt = await web3.Privacy.GetPrivateTransactionReceipt.SendRequestAsync(privacyMarkerTransactionHash);
```

Also: `DistributePrivateTransaction`, `FillTransaction`, `GetContractPrivacyMetadata`,
`GetPrivacyPrecompileAddress`, `GetPrivateTransactionByHash`, `GetPsi`, `GetQuorumPayload`,
`SendRawPrivateTransaction`, `SendTransaction`.

**Raft** (`src/Nethereum.Quorum/RPC/Services/IRaftService.cs`):

```csharp
var role = await web3.Raft.Role.SendRequestAsync(); // "minter" / "verifier" / "learner"
```

Also: `AddLearner`, `AddPeer`, `AddCluster`, `Leader`, `PromoteToPeer`, `RemovePeer`.

**IBFT** (Istanbul BFT consensus, `src/Nethereum.Quorum/RPC/Services/IIBFTService.cs`):

```csharp
var validators = await web3.IBFT.GetValidators.SendRequestAsync(BlockParameter.CreateLatest());
```

Also: `Candidates`, `Discard`, `GetSignersFromBlock`, `GetSignersFromBlockByHash`, `GetSnapshot`,
`GetSnapshotAtHash`, `GetValidatorsAtHash`, `IsValidator`, `NodeAddress`, `Propose`, `Status`.

**ContractExtensions** (extend a private contract to a new participant, `src/Nethereum.Quorum/RPC/Services/IContractExtensionsService.cs`):

```csharp
var status = await web3.ContractExtensions.GetExtensionStatus.SendRequestAsync(managementContractAddress);
```

Also: `ActiveExtensionContracts`, `ApproveExtension`, `CancelExtension`, `ExtendContract`.

**DebugQuorum** (`src/Nethereum.Quorum/RPC/Services/IDebugQuorumService.cs`):

```csharp
var state = await web3.DebugQuorum.DebugDumpAddress.SendRequestAsync(address, BlockParameter.CreateLatest());
```

Also: `DebugPrivateStateRoot`.

### QuorumAccount, QuorumTransactionManager, UnlockedAccount

`QuorumAccount` (`src/Nethereum.Quorum/QuorumAccount.cs`) is an `Account` whose default transaction manager is
`QuorumTransactionManager` (`src/Nethereum.Quorum/QuorumTransactionManager.cs`), which understands
`PrivateFor`/`PrivateFrom` and applies Quorum's `v` value shift for private transactions.
`UnlockedAccount` (`src/Nethereum.Quorum/UnlockedAccount.cs`) represents an account already unlocked on the
node itself (no local private key). `Web3Quorum` accepts either:

```csharp
using System;
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum;

// Client-side signing with a private key; PrivateUrl targets the node's private transaction manager (Tessera/Constellation)
var account = new QuorumAccount("PRIVATE_KEY");
var web3 = new Web3Quorum(account, privateUrl: "http://localhost:9081", url: "http://localhost:22000");

// Node-side signing (account already unlocked on the Quorum node)
var unlockedAccount = new UnlockedAccount("0xNodeUnlockedAddress");
var web3Unlocked = new Web3Quorum(new RpcClient(new Uri("http://localhost:22000")), unlockedAccount);
```