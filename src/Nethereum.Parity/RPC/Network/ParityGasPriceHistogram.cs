using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.Network
{

    ///<Summary>
    /// parity_gasPriceHistogram
/// 
/// Returns a snapshot of the historic gas prices.
/// 
/// Parameters
/// 
/// None
/// 
/// Returns
/// 
/// Object - Historic values
/// bucketBounds: Array - Array of bound values.
/// count: Array - Array of counts.
/// Example
/// 
/// Request
/// 
/// curl --data '{"method":"parity_gasPriceHistogram","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
/// Response
/// 
/// {
///   "id": 1,
///   "jsonrpc": "2.0",
///   "result": {
///     "bucketBounds": [
///       "0x4a817c800",
///       "0x525433d01",
///       "0x5a26eb202",
///       "0x61f9a2703",
///       "0x69cc59c04",
///       "0x719f11105",
///       "0x7971c8606",
///       "0x81447fb07",
///       "0x891737008",
///       "0x90e9ee509",
///       "0x98bca5a0a"
///     ],
///     "counts": [
///       487,
///       9,
///       7,
///       1,
///       8,
///       0,
///       0,
///       0,
///       0,
///       14
///     ]
///   }
/// }    
    ///</Summary>
    public class ParityGasPriceHistogram : GenericRpcRequestResponseHandlerNoParam<string>
    {
            public ParityGasPriceHistogram(IClient client) : base(client, ApiMethods.parity_gasPriceHistogram.ToString()) { }
    }

}
            
        