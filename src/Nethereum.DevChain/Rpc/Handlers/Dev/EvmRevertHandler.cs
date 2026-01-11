using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Extensions;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class EvmRevertHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.evm_revert.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var devNode = (DevChainNode)context.Node;
            var snapshotIdHex = GetParam<string>(request, 0);
            var snapshotId = (int)snapshotIdHex.HexToBigInteger(false);

            var snapshot = new SnapshotReference(snapshotId);
            await devNode.RevertToSnapshotAsync(snapshot);

            return Success(request.Id, true);
        }
    }

    internal class SnapshotReference : IStateSnapshot
    {
        public int SnapshotId { get; }

        public SnapshotReference(int id)
        {
            SnapshotId = id;
        }

        public void SetAccount(string address, Account account) { }
        public void SetStorage(string address, BigInteger slot, byte[] value) { }
        public void SetCode(byte[] codeHash, byte[] code) { }
        public void DeleteAccount(string address) { }
        public void ClearStorage(string address) { }
        public void Dispose() { }
    }
}
