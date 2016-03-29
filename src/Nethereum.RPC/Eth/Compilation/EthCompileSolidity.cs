using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Compilation
{
    /// <summary>
    ///     Returns compiled solidity code.
    ///     Parameters
    ///     1. String - The source code.
    ///     params: [
    ///     "contract test { function multiply(uint a) returns(uint d) {   return a * 7;   } }",
    ///     ]
    ///     Returns
    ///     DATA - The compiled source code.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_compileSolidity","params":["contract test { function
    ///     multiply(uint a) returns(uint d) {   return a * 7;   } }"],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result": {
    ///     "code":
    ///     "0x605880600c6000396000f3006000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa114602e57005b603d6004803590602001506047565b8060005260206000f35b60006007820290506053565b91905056",
    ///     "info": {
    ///     "source": "contract test {\n   function multiply(uint a) constant returns(uint d) {\n       return a * 7;\n
    ///     }\n}\n",
    ///     "language": "Solidity",
    ///     "languageVersion": "0",
    ///     "compilerVersion": "0.9.19",
    ///     "abiDefinition": [
    ///     {
    ///     "constant": true,
    ///     "inputs": [
    ///     {
    ///     "name": "a",
    ///     "type": "uint256"
    ///     }
    ///     ],
    ///     "name": "multiply",
    ///     "outputs": [
    ///     {
    ///     "name": "d",
    ///     "type": "uint256"
    ///     }
    ///     ],
    ///     "type": "function"
    ///     }
    ///     ],
    ///     "userDoc": {
    ///     "methods": {}
    ///     },
    ///     "developerDoc": {
    ///     "methods": {}
    ///     }
    ///     }
    ///     }
    /// </summary>
    public class EthCompileSolidity : RpcRequestResponseHandler<string>
    {
        public EthCompileSolidity(IClient client) : base(client, ApiMethods.eth_compileSolidity.ToString())
        {
        }

        public async Task<string> SendRequestAsync(string contractCode, object id = null)
        {
            return await base.SendRequestAsync(id, contractCode);
        }

        public RpcRequest BuildRequest(string contractCode, object id = null)
        {
            return base.BuildRequest(id, contractCode);
        }
    }
}