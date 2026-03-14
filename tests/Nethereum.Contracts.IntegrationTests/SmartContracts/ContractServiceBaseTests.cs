using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Documentation;
using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
{
    public class ContractServiceBaseTests
    {
        [Event("Transfer")]
        public class TransferEventDTO : IEventDTO
        {
            [Parameter("address", "_from", 1, true)]
            public string From { get; set; }

            [Parameter("address", "_to", 2, true)]
            public string To { get; set; }

            [Parameter("uint256", "_value", 3, false)]
            public BigInteger Value { get; set; }
        }

        [Event("Approval")]
        public class ApprovalEventDTO : IEventDTO
        {
            [Parameter("address", "_owner", 1, true)]
            public string Owner { get; set; }

            [Parameter("address", "_spender", 2, true)]
            public string Spender { get; set; }

            [Parameter("uint256", "_value", 3, false)]
            public BigInteger Value { get; set; }
        }

        [Function("balanceOf", "uint256")]
        public class BalanceOfFunction : FunctionMessage
        {
            [Parameter("address", "_owner", 1)]
            public string Owner { get; set; }
        }

        [Function("transfer", "bool")]
        public class TransferFunction : FunctionMessage
        {
            [Parameter("address", "_to", 1)]
            public string To { get; set; }

            [Parameter("uint256", "_value", 2)]
            public BigInteger Value { get; set; }
        }

        [Error("InsufficientBalance")]
        public class InsufficientBalanceError
        {
            [Parameter("address", "account", 1)]
            public string Account { get; set; }

            [Parameter("uint256", "balance", 2)]
            public BigInteger Balance { get; set; }
        }

        public class TestTokenService : ContractServiceBase
        {
            public TestTokenService(Web3.IWeb3 web3, string contractAddress)
            {
                ContractHandler = web3.Eth.GetContractHandler(contractAddress);
            }

            public override List<Type> GetAllFunctionTypes()
            {
                return new List<Type> { typeof(BalanceOfFunction), typeof(TransferFunction) };
            }

            public override List<Type> GetAllEventTypes()
            {
                return new List<Type> { typeof(TransferEventDTO), typeof(ApprovalEventDTO) };
            }

            public override List<Type> GetAllErrorTypes()
            {
                return new List<Type> { typeof(InsufficientBalanceError) };
            }
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "ContractServiceBase: introspect functions, events, errors", SkillName = "built-in-standards", Order = 9)]
        public void ShouldIntrospectAllRegisteredTypes()
        {
            var web3 = new Web3.Web3();
            var service = new TestTokenService(web3, "0x0000000000000000000000000000000000000001");

            var functionTypes = service.GetAllFunctionTypes();
            Assert.Equal(2, functionTypes.Count);
            Assert.Contains(typeof(BalanceOfFunction), functionTypes);
            Assert.Contains(typeof(TransferFunction), functionTypes);

            var eventTypes = service.GetAllEventTypes();
            Assert.Equal(2, eventTypes.Count);
            Assert.Contains(typeof(TransferEventDTO), eventTypes);
            Assert.Contains(typeof(ApprovalEventDTO), eventTypes);

            var errorTypes = service.GetAllErrorTypes();
            Assert.Single(errorTypes);
            Assert.Contains(typeof(InsufficientBalanceError), errorTypes);

            var functionABIs = service.GetAllFunctionABIs();
            Assert.Equal(2, functionABIs.Count);

            var eventABIs = service.GetAllEventABIs();
            Assert.Equal(2, eventABIs.Count);

            var errorABIs = service.GetAllErrorABIs();
            Assert.Single(errorABIs);

            var functionSigs = service.GetAllFunctionSignatures();
            Assert.Equal(2, functionSigs.Length);

            var eventSigs = service.GetAllEventsSignatures();
            Assert.Equal(2, eventSigs.Length);

            var errorSigs = service.GetAllErrorsSignatures();
            Assert.Single(errorSigs);
        }
    }
}
