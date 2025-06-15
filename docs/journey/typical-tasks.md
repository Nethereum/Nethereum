# Typical Tasks

- Transfer ETH
- Call contract functions
- Decode events
- Estimate gas

```csharp
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xaddress", 0.1m);
```
