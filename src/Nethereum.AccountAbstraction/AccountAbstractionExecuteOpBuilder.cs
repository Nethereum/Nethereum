using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Nethereum.AccountAbstraction
{
    public class AccountAbstractionExecuteOpBuilder
    {

        /*   function executeUserOp(
                    PackedUserOperation calldata userOp,
                    bytes32 userOpHash
                ) external;
         }*/
        public class ExecuteOpFunction: FunctionMessage
        {
            [Parameter("tuple", "userOp", 1)]
            public virtual PackedUserOperation UserOp { get; set; }
            [Parameter("bytes32", "userOpHash", 2)]
            public virtual byte[] UserOpHash { get; set; }

        }

        public byte[] CreateAccountAbstractionExecuteOpEncodedFunction(PackedUserOperation userOp, byte[] userOpHash)
        {
            var executeOpFunction = new ExecuteOpFunction();
            executeOpFunction.UserOp = userOp;
            executeOpFunction.UserOpHash = userOpHash;

            return executeOpFunction.GetCallData();

        }

        public byte[] CreateAccountAbstractionExecuteOpEncodedFunctionAsync(PackedUserOperation userOp, string entryPoint, BigInteger chainId)
        {
            var hash = UserOperationBuilder.HashUserOperation(userOp, entryPoint, chainId);    
            return CreateAccountAbstractionExecuteOpEncodedFunction(userOp, hash);
        }

    }
}