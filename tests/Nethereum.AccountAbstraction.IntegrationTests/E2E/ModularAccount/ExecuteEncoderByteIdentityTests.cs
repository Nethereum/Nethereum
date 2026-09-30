using System.Collections.Generic;
using System.Numerics;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.Execution;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using GeneratedSimpleAccountExecuteFunction =
    Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteFunction;
using GeneratedSimpleAccountExecuteBatchFunction =
    Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteBatchFunction;
using SimpleAccountCall =
    Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.Call;
using GeneratedNethereumAccountExecuteFunction =
    Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition.ExecuteFunction;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    public class ExecuteEncoderByteIdentityTests
    {
        private const string Target = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
        private const string Target2 = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        [Fact]
        [Trait("UseCase", "CustomContract")]
        public void SimpleAccountExecuteEncoder_EncodeExecute_Matches_GeneratedExecuteFunction()
        {
            BigInteger value = 123;
            var callData = "0xabcdef".HexToByteArray();

            var actual = new SimpleAccountExecuteEncoder().EncodeExecute(Target, value, callData);

            var expected = new GeneratedSimpleAccountExecuteFunction
            {
                Target = Target,
                Value = value,
                Data = callData
            }.GetCallData();

            Assert.Equal(expected.ToHex(), actual.ToHex());
        }

        [Fact]
        [Trait("UseCase", "CustomContract")]
        public void SimpleAccountExecuteEncoder_EncodeBatch_Matches_GeneratedExecuteBatchFunction()
        {
            var callData1 = "0x1111".HexToByteArray();
            var callData2 = "0x2222".HexToByteArray();

            var calls = new List<(string target, BigInteger value, byte[] callData)>
            {
                (Target, 1, callData1),
                (Target2, 2, callData2)
            };

            var actual = new SimpleAccountExecuteEncoder().EncodeBatch(calls);

            var expected = new GeneratedSimpleAccountExecuteBatchFunction
            {
                Calls = new List<SimpleAccountCall>
                {
                    new SimpleAccountCall { Target = Target, Value = 1, Data = callData1 },
                    new SimpleAccountCall { Target = Target2, Value = 2, Data = callData2 }
                }
            }.GetCallData();

            Assert.Equal(expected.ToHex(), actual.ToHex());
        }

        [Fact]
        [Trait("UseCase", "CustomContract")]
        public void Erc7579ExecuteEncoder_EncodeExecute_Matches_NethereumAccountService_ExecuteAsync_Encoding()
        {
            BigInteger value = 456;
            var callData = "0x1234".HexToByteArray();

            var actual = new Erc7579ExecuteEncoder().EncodeExecute(Target, value, callData);

            var expectedMode = ERC7579ModeLib.EncodeSingleDefault();
            var expectedExecutionCalldata = ERC7579ExecutionLib.EncodeSingle(Target, value, callData);
            var expected = new GeneratedNethereumAccountExecuteFunction
            {
                Mode = expectedMode,
                ExecutionCalldata = expectedExecutionCalldata
            }.GetCallData();

            Assert.Equal(expected.ToHex(), actual.ToHex());
        }

        [Fact]
        [Trait("UseCase", "CustomContract")]
        public void Erc7579ExecuteEncoder_EncodeBatch_Matches_NethereumAccountService_ExecuteBatchAsync_Encoding()
        {
            var callData1 = "0xaaaa".HexToByteArray();
            var callData2 = "0xbbbb".HexToByteArray();

            var calls = new List<(string target, BigInteger value, byte[] callData)>
            {
                (Target, 10, callData1),
                (Target2, 20, callData2)
            };

            var actual = new Erc7579ExecuteEncoder().EncodeBatch(calls);

            var expectedMode = ERC7579ModeLib.EncodeBatchDefault();
            var expectedExecutionCalldata = ERC7579ExecutionLib.EncodeBatch(new[]
            {
                new Call { Target = Target, Value = 10, Data = callData1 },
                new Call { Target = Target2, Value = 20, Data = callData2 }
            });
            var expected = new GeneratedNethereumAccountExecuteFunction
            {
                Mode = expectedMode,
                ExecutionCalldata = expectedExecutionCalldata
            }.GetCallData();

            Assert.Equal(expected.ToHex(), actual.ToHex());
        }
    }
}
