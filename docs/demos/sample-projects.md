## Sample Projects

### Nethereum Wallet

This is a cross platform wallet example using Nethereum, Xamarin.Forms and MvvmCross, targeting all main mobile platforms Android, iOS, Windows, Desktop (Windows 10 uwp), IoT with the Raspberry PI and Xbox.

[Source Code](https://github.com/Nethereum/Nethereum.UI.Wallet.Sample)

### Nethereum Web 

This example demonstrates using Nethereum on a web project and how you can interact with the DAO.

[Source Code](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Web.Sample)


![Nethereum Web Sample](https://raw.githubusercontent.com/Nethereum/Nethereum/master/docs/screenshots/websample.png)

The sample uses a simple pattern to create a contract service, instantiating it with an instance of Web3 and the contract address.

```csharp
public class DaoService
{
    private readonly Web3.Web3 web3;
    private string abi = @"abi...";
    private Contract contract;
    public DaoService(Web3.Web3 web3, string address)
    {
        this.web3 = web3;
        this.contract = web3.Eth.GetContract(abi, address);
    }
```

The DAO stores the total number of proposals by declaring a public attribute, this can be accessed as follows:

```csharp
    public Task<long> GetNumberOfProposals()
    {
        return contract.GetFunction("numberOfProposals").CallAsync<long>();
    }
```

If you want to retrieve all the Proposals you can iterate from 0 to the total number of proposals and add them to a collection as:

```csharp
    public async Task<List<Proposal>> GetAllProposals()
    {

        var numberOfProposals = await GetNumberOfProposals().ConfigureAwait(false);
        var proposals = new List<Proposal>();

        for (var i = 0; i < numberOfProposals; i++)
        {
            proposals.Add(await GetProposal(i).ConfigureAwait(false));
        }
        return proposals;
    }
```

What is missing above is how to get that specific Proposal data to so you can call the function "proposals" this is in solidity the "mapping" of the proposal number and the struct proposal, which are output parameters.

```csharp
   public async Task<Proposal> GetProposal(long index)
    {
        var proposalsFunction = contract.GetFunction("proposals");
        var proposal = await proposalsFunction.CallDeserializingToObjectAsync<Proposal>(index).ConfigureAwait(false);
        proposal.Index = index;
        return proposal;
    }
```

Above you may have noticed the output is deserialised into a Proposal Object, this is the FunctionOuput and requires information (from the ABI) to do the deserialisation.

```csharp
[FunctionOutput]
public class Proposal
{
    public long Index { get; set; }

    [Parameter("address", 1)]
    public string Recipient { get; set; }

    [Parameter("uint256", 2)]
    public BigInteger Amount { get; set; }

    [Parameter("string", 3)]
    public string Description { get; set; }

    [Parameter("uint256", 4)]
    public BigInteger VotingDeadline { get; set; }

    [Parameter("bool", 5)]
    public bool Open { get; set; }

    [Parameter("bool", 6)]
    public bool ProposalPassed { get; set; }

    [Parameter("bytes32", 7)]
    public byte[] ProposalHash { get; set; }

    public string GetProposalHashToHex()
    {
        return ProposalHash.ToHex();
    }

    [Parameter("uint256", 8)]
    public BigInteger ProposalDeposit { get; set; }

    [Parameter("bool", 9)]
    public bool NewCurator { get; set; }

    [Parameter("uint256", 10)]
    public BigInteger Yea { get; set; }

    [Parameter("uint256", 11)]
    public BigInteger Nay { get; set; }

    [Parameter("address", 12)]
    public string Creator { get; set; }
}
```
