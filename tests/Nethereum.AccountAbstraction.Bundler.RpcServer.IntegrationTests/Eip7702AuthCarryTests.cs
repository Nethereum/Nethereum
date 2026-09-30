using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    /// <summary>
    /// The EIP-7702 auth tuple is a side-channel: it must survive the RPC to domain hop
    /// (ToDomainUserOperation) even though it never reaches the packed form.
    /// Pure unit coverage of the static extension — no server required.
    /// </summary>
    public class Eip7702AuthCarryTests
    {
        [Fact]
        public void ToDomainUserOperation_CarriesEip7702Auth_RpcToClient()
        {
            var auth = new Authorisation
            {
                ChainId = new HexBigInteger(1),
                Address = "0x1234567890123456789012345678901234567890",
                Nonce = new HexBigInteger(7),
                YParity = "0x1",
                R = "0x1111111111111111111111111111111111111111111111111111111111111111",
                S = "0x2222222222222222222222222222222222222222222222222222222222222222"
            };

            var rpcUserOp = new RpcUserOperation
            {
                Sender = "0x1111111111111111111111111111111111111111",
                Nonce = new HexBigInteger(1),
                Eip7702Auth = auth
            };

            var domain = rpcUserOp.ToDomainUserOperation();

            Assert.Same(auth, domain.Eip7702Auth);
        }

        [Fact]
        public void ToDomainUserOperation_LeavesEip7702AuthNull_WhenUnset()
        {
            var rpcUserOp = new RpcUserOperation
            {
                Sender = "0x1111111111111111111111111111111111111111",
                Nonce = new HexBigInteger(1)
            };

            var domain = rpcUserOp.ToDomainUserOperation();

            Assert.Null(domain.Eip7702Auth);
        }
    }
}
