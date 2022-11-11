using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Network
{
    /// <Summary>
    ///     parity_pendingTransactions
    ///     Returns a list of transactions currently in the queue.
    ///     Parameters
    ///     None
    ///     Returns
    ///     Array - Transactions ordered by priority
    ///     hash: Hash - 32 Bytes - hash of the transaction.
    ///     nonce: Quantity - The number of transactions made by the sender prior to this one.
    ///     blockHash: Hash - 32 Bytes - hash of the block where this transaction was in. null when its pending.
    ///     blockNumber: Quantity | Tag - Block number where this transaction was in. null when its pending.
    ///     transactionIndex: Quantity - Integer of the transactions index position in the block. null when its pending.
    ///     from: Address - 20 Bytes - address of the sender.
    ///     to: Address - 20 Bytes - address of the receiver. null when its a contract creation transaction.
    ///     value: Quantity - Value transferred in Wei.
    ///     gasPrice: Quantity - Gas price provided by the sender in Wei.
    ///     gas: Quantity - Gas provided by the sender.
    ///     input: Data - The data send along with the transaction.
    ///     creates: Address - (optional) Address of a created contract or null.
    ///     raw: Data - Raw transaction data.
    ///     publicKey: Data - Public key of the signer.
    ///     networkId: Quantity - The network id of the transaction, if any.
    ///     standardV: Quantity - The standardized V field of the signature (0 or 1).
    ///     v: Quantity - The V field of the signature.
    ///     r: Quantity - The R field of the signature.
    ///     s: Quantity - The S field of the signature.
    ///     condition: Object - (optional) Conditional submission, Block number in block or timestamp in time or null.
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_pendingTransactions","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type:
    ///     application/json" -X POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": [
    ///     {
    ///     "blockHash": null,
    ///     "blockNumber": null,
    ///     "creates": null,
    ///     "from": "0xee3ea02840129123d5397f91be0391283a25bc7d",
    ///     "gas": "0x23b58",
    ///     "gasPrice": "0xba43b7400",
    ///     "hash": "0x160b3c30ab1cf5871083f97ee1cee3901cfba3b0a2258eb337dd20a7e816b36e",
    ///     "input":
    ///     "0x095ea7b3000000000000000000000000bf4ed7b27f1d666546e30d74d50d173d20bca75400000000000000000000000000002643c948210b4bd99244ccd64d5555555555",
    ///     "minBlock": null,
    ///     "networkId": 1,
    ///     "nonce": "0x5",
    ///     "publicKey":
    ///     "0x96157302dade55a1178581333e57d60ffe6fdf5a99607890456a578b4e6b60e335037d61ed58aa4180f9fd747dc50d44a7924aa026acbfb988b5062b629d6c36",
    ///     "r": "0x92e8beb19af2bad0511d516a86e77fa73004c0811b2173657a55797bdf8558e1",
    ///     "raw":
    ///     "0xf8aa05850ba43b740083023b5894bb9bc244d798123fde783fcc1c72d3bb8c18941380b844095ea7b3000000000000000000000000bf4ed7b27f1d666546e30d74d50d173d20bca75400000000000000000000000000002643c948210b4bd99244ccd64d555555555526a092e8beb19af2bad0511d516a86e77fa73004c0811b2173657a55797bdf8558e1a062b4d4d125bbcb9c162453bc36ca156537543bb4414d59d1805d37fb63b351b8",
    ///     "s": "0x62b4d4d125bbcb9c162453bc36ca156537543bb4414d59d1805d37fb63b351b8",
    ///     "standardV": "0x1",
    ///     "to": "0xbb9bc244d798123fde783fcc1c72d3bb8c189413",
    ///     "transactionIndex": null,
    ///     "v": "0x26",
    ///     "value": "0x0"
    ///     },
    ///     { ... },
    ///     { ... }
    ///     ]
    ///     }
    /// </Summary>
    public class ParityPendingTransactions : GenericRpcRequestResponseHandlerNoParam<JArray>, IParityPendingTransactions
    {
        public ParityPendingTransactions(IClient client) : base(client,
            ApiMethods.parity_pendingTransactions.ToString())
        {
        }
    }

    public interface IParityPendingTransactions : IGenericRpcRequestResponseHandlerNoParam<JArray>
    {


    }
}