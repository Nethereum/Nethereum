using System;
using System.Linq;
using Nethereum.CoreChain.Forks;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Forks
{
    public class SystemContractPredeployEipPinningTests
    {
        private const string Eip4788DeploymentTransactionInput =
            "0x60618060095f395ff33373fffffffffffffffffffffffffffffffffffffffe14604d57602036146024575f5ffd5b5f" +
            "35801560495762001fff810690815414603c575f5ffd5b62001fff01545f5260205ff35b5f5ffd5b62001fff42064281" +
            "555f359062001fff015500";
        private const string Eip4788ConstructorPrefix =
            "0x60618060095f395ff3";

        private const string Eip2935DeploymentTransactionInput =
            "0x60538060095f395ff33373fffffffffffffffffffffffffffffffffffffffe14604657602036036042575f35600143" +
            "038111604257611fff81430311604257611fff9006545f5260205ff35b5f5ffd5b5f35611fff60014303065500";
        private const string Eip2935ConstructorPrefix =
            "0x60538060095f395ff3";

        private const string Eip7002DeploymentTransactionInput =
            "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff5f556101f880602d5f395ff33373" +
            "fffffffffffffffffffffffffffffffffffffffe1460cb5760115f54807fffffffffffffffffffffffffffffffffffff" +
            "ffffffffffffffffffffffffffff146101f457600182026001905f5b5f82111560685781019083028483029004916001" +
            "019190604d565b909390049250505036603814608857366101f457346101f4575f5260205ff35b34106101f457600154" +
            "600101600155600354806003026004013381556001015f35815560010160203590553360601b5f5260385f601437604c" +
            "5fa0600101600355005b6003546002548082038060101160df575060105b5f5b81811461018357828101600302600401" +
            "81604c02815460601b8152601401816001015481526020019060020154807fffffffffffffffffffffffffffffffff00" +
            "000000000000000000000000000000168252906010019060401c908160381c81600701538160301c8160060153816028" +
            "1c81600501538160201c81600401538160181c81600301538160101c81600201538160081c81600101535360010160e1" +
            "565b910180921461019557906002556101a0565b90505f6002555f6003555b5f54807fffffffffffffffffffffffffff" +
            "ffffffffffffffffffffffffffffffffffffff14156101cd57505f5b6001546002828201116101e25750505f6101e856" +
            "5b01600290035b5f555f600155604c025ff35b5f5ffd";
        private const string Eip7002ConstructorPrefix =
            "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff5f556101f880602d5f395ff3";

        private const string Eip7251DeploymentTransactionInput =
            "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff5f5561019e80602d5f395ff33373" +
            "fffffffffffffffffffffffffffffffffffffffe1460d35760115f54807fffffffffffffffffffffffffffffffffffff" +
            "ffffffffffffffffffffffffffff1461019a57600182026001905f5b5f82111560685781019083028483029004916001" +
            "019190604d565b9093900492505050366060146088573661019a573461019a575f5260205ff35b341061019a57600154" +
            "600101600155600354806004026004013381556001015f358155600101602035815560010160403590553360601b5f52" +
            "60605f60143760745fa0600101600355005b6003546002548082038060021160e7575060025b5f5b8181146101295782" +
            "810160040260040181607402815460601b81526014018160010154815260200181600201548152602001906003015490" +
            "5260010160e9565b910180921461013b5790600255610146565b90505f6002555f6003555b5f54807fffffffffffffff" +
            "ffffffffffffffffffffffffffffffffffffffffffffffffff141561017357505f5b6001546001828201116101885750" +
            "505f61018e565b01600190035b5f555f6001556074025ff35b5f5ffd";
        private const string Eip7251ConstructorPrefix =
            "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff5f5561019e80602d5f395ff3";

        private const string Eip8282SysAsmBuilderDepositsMainRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe1461011c575f54807fffffffffffffffffffffffffffffffff" +
            "ffffffffffffffffffffffffffffffff146102705760015460088111605257506058565b60089003015b601190600182" +
            "026001905f5b5f821115607f57810190830284830290049160010191906064565b90939004925050503660b814609f57" +
            "366102705734610270575f5260205ff35b8034106102705760383567ffffffffffffffff1680633b9aca001161027057" +
            "633b9aca00029034031061027057600154600101600155600354806006026004015f3581556001016020358155600101" +
            "60403581556001016060358155600101608035815560010160a035905560b85f5f3760b85fa0600101600355005b6003" +
            "5460025480820380604011610131575060405b5f5b8181146101d7578281016006026004018160b80281548152602001" +
            "81600101548152602001816002015480825260401c67ffffffffffffffff16816010018160381c81600701538160301c" +
            "81600601538160281c81600501538160201c81600401538160181c81600301538160101c81600201538160081c816001" +
            "015353602001816003015481526020018160040154815260200190600501549052600101610133565b91018092146101" +
            "e957906002556101f4565b90505f6002555f6003555b36610242575f54600154817fffffffffffffffffffffffffffff" +
            "ffffffffffffffffffffffffffffffffffff1461023057600882820111610238575b50505f610264565b016008900361" +
            "0264565b7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff5b5f555f60015560b8025f" +
            "f35b5f5ffd";

        private const string Eip8282SysAsmBuilderExitsMainRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe1460e1575f54807fffffffffffffffffffffffffffffffffff" +
            "ffffffffffffffffffffffffffffff146101c65760015460028111605157506057565b60029003015b60119060018202" +
            "6001905f5b5f821115607e57810190830284830290049160010191906063565b909390049250505036603014609e5736" +
            "6101c657346101c6575f5260205ff35b34106101c657600154600101600155600354806003026004013381556001015f" +
            "35815560010160203590553360601b5f5260305f60143760445fa0600101600355005b60035460025480820380601011" +
            "60f5575060105b5f5b81811461012d5782810160030260040181604402815460601b8152601401816001015481526020" +
            "019060020154905260010160f7565b910180921461013f579060025561014a565b90505f6002555f6003555b36610198" +
            "575f54600154817fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff1461018657600282" +
            "82011161018e575b50505f6101ba565b01600290036101ba565b7fffffffffffffffffffffffffffffffffffffffffff" +
            "ffffffffffffffffffffff5b5f555f6001556044025ff35b5f5ffd";

        private const string Eip7997PublishedFactoryRuntimeCode =
            "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe03601600081602082378035828234" +
            "f58015156039578182fd5b8082525050506014600cf3";
        private const string Eip4788BeaconRootsAddress = "0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02";
        private const string Eip2935HistoryStorageAddress = "0x0000F90827F1C53a10cb7A02335B175320002935";
        private const string Eip7002WithdrawalRequestPredeployAddress = "0x00000961Ef480Eb55e80D19ad83579A64c007002";
        private const string Eip7251ConsolidationRequestPredeployAddress = "0x0000BBdDc7CE488642fb579F8B00f3a590007251";
        private const string Eip8282BuilderDepositContractAddress = "0x0000bFF46984e3725691FA540a8C7589300D8282";
        private const string Eip8282BuilderExitContractAddress = "0x000064D678505ad48F8cCb093BC65613800E8282";
        private const string Eip7997FactoryAddress = "0x4e59b44847b379578588920cA78FbF26c0B4956C";

        [Fact]
        public void Beacon_roots_runtime_code_is_the_code_the_eip4788_deployment_transaction_returns()
        {
            AssertRuntimeCodeIsDeploymentTail(
                Eip4788DeploymentTransactionInput,
                Eip4788ConstructorPrefix,
                SystemContractPredeploys.BeaconRootsRuntimeCode);
        }

        [Fact]
        public void History_storage_runtime_code_is_the_code_the_eip2935_deployment_transaction_returns()
        {
            AssertRuntimeCodeIsDeploymentTail(
                Eip2935DeploymentTransactionInput,
                Eip2935ConstructorPrefix,
                SystemContractPredeploys.HistoryStorageRuntimeCode);
        }

        [Fact]
        public void Withdrawal_requests_runtime_code_is_the_code_the_eip7002_deployment_transaction_returns()
        {
            AssertRuntimeCodeIsDeploymentTail(
                Eip7002DeploymentTransactionInput,
                Eip7002ConstructorPrefix,
                SystemContractPredeploys.WithdrawalRequestsRuntimeCode);
        }

        [Fact]
        public void Consolidation_requests_runtime_code_is_the_code_the_eip7251_deployment_transaction_returns()
        {
            AssertRuntimeCodeIsDeploymentTail(
                Eip7251DeploymentTransactionInput,
                Eip7251ConstructorPrefix,
                SystemContractPredeploys.ConsolidationRequestsRuntimeCode);
        }

        [Fact]
        public void Builder_deposit_runtime_code_is_the_runtime_code_eip8282_adopts_by_reference()
        {
            Assert.Equal(
                Hex(Eip8282SysAsmBuilderDepositsMainRuntimeCode),
                Hex(SystemContractPredeploys.BuilderDepositRuntimeCode));
        }

        [Fact]
        public void Builder_exit_runtime_code_is_the_runtime_code_eip8282_adopts_by_reference()
        {
            Assert.Equal(
                Hex(Eip8282SysAsmBuilderExitsMainRuntimeCode),
                Hex(SystemContractPredeploys.BuilderExitRuntimeCode));
        }

        [Fact]
        public void Create2_factory_runtime_code_is_the_runtime_code_eip7997_publishes()
        {
            Assert.Equal(
                Hex(Eip7997PublishedFactoryRuntimeCode),
                Hex(SystemContractPredeploys.Create2FactoryRuntimeCode));
        }

        [Fact]
        public void Predeploy_addresses_are_the_addresses_their_eips_specify()
        {
            AssertSameAddress(Eip4788BeaconRootsAddress, Eip4788Constants.BeaconRootsAddress);
            AssertSameAddress(Eip2935HistoryStorageAddress, Eip2935Constants.HistoryStorageAddress);
            AssertSameAddress(Eip7002WithdrawalRequestPredeployAddress, SystemCallContracts.WithdrawalRequests);
            AssertSameAddress(Eip7251ConsolidationRequestPredeployAddress, SystemCallContracts.ConsolidationRequests);
            AssertSameAddress(Eip8282BuilderDepositContractAddress, SystemCallContracts.BuilderDeposit);
            AssertSameAddress(Eip8282BuilderExitContractAddress, SystemCallContracts.BuilderExit);
            AssertSameAddress(Eip7997FactoryAddress, SystemContractPredeploys.Create2FactoryAddress);
        }

        [Fact]
        public void The_two_copies_of_each_system_contract_address_still_name_the_same_account()
        {
            AssertSameAddress(Eip4788Constants.BeaconRootsAddress, SystemCallContracts.BeaconRoots);
            AssertSameAddress(Eip2935Constants.HistoryStorageAddress, SystemCallContracts.HistoryStorage);
        }

        [Fact]
        public void Every_predeploy_pairs_its_eip_address_with_its_eip_runtime_code_and_activation_fork()
        {
            var expected = new[]
            {
                (Eip4788BeaconRootsAddress, DeploymentTail(Eip4788DeploymentTransactionInput, Eip4788ConstructorPrefix), HardforkName.Cancun),
                (Eip2935HistoryStorageAddress, DeploymentTail(Eip2935DeploymentTransactionInput, Eip2935ConstructorPrefix), HardforkName.Prague),
                (Eip7002WithdrawalRequestPredeployAddress, DeploymentTail(Eip7002DeploymentTransactionInput, Eip7002ConstructorPrefix), HardforkName.Prague),
                (Eip7251ConsolidationRequestPredeployAddress, DeploymentTail(Eip7251DeploymentTransactionInput, Eip7251ConstructorPrefix), HardforkName.Prague),
                (Eip8282BuilderDepositContractAddress, Hex(Eip8282SysAsmBuilderDepositsMainRuntimeCode), HardforkName.Amsterdam),
                (Eip8282BuilderExitContractAddress, Hex(Eip8282SysAsmBuilderExitsMainRuntimeCode), HardforkName.Amsterdam),
                (Eip7997FactoryAddress, Hex(Eip7997PublishedFactoryRuntimeCode), HardforkName.Amsterdam),
            };

            var actual = SystemContractPredeploys.All
                .Select(predeploy => (predeploy.Address, ToHex(predeploy.RuntimeCode), predeploy.ActivationFork))
                .ToArray();

            Assert.Equal(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; i++)
            {
                AssertSameAddress(expected[i].Item1, actual[i].Item1);
                Assert.Equal(expected[i].Item2, actual[i].Item2);
                Assert.Equal(expected[i].Item3, actual[i].Item3);
            }
        }

        [Fact]
        public void One_flipped_byte_in_a_blob_or_an_address_fails_the_pin()
        {
            Assert.ThrowsAny<Exception>(() => AssertRuntimeCodeIsDeploymentTail(
                Eip4788DeploymentTransactionInput,
                Eip4788ConstructorPrefix,
                FlipFirstByte(SystemContractPredeploys.BeaconRootsRuntimeCode)));

            Assert.ThrowsAny<Exception>(() => Assert.Equal(
                Hex(Eip8282SysAsmBuilderDepositsMainRuntimeCode),
                Hex(FlipFirstByte(SystemContractPredeploys.BuilderDepositRuntimeCode))));

            Assert.ThrowsAny<Exception>(() => AssertSameAddress(
                Eip7997FactoryAddress,
                FlipFirstByte(SystemContractPredeploys.Create2FactoryAddress)));
        }

        [Fact]
        public void Every_pinned_blob_is_the_length_its_eip_deploys()
        {
            Assert.Equal(97, Bytes(SystemContractPredeploys.BeaconRootsRuntimeCode).Length);
            Assert.Equal(83, Bytes(SystemContractPredeploys.HistoryStorageRuntimeCode).Length);
            Assert.Equal(504, Bytes(SystemContractPredeploys.WithdrawalRequestsRuntimeCode).Length);
            Assert.Equal(414, Bytes(SystemContractPredeploys.ConsolidationRequestsRuntimeCode).Length);
            Assert.Equal(628, Bytes(SystemContractPredeploys.BuilderDepositRuntimeCode).Length);
            Assert.Equal(458, Bytes(SystemContractPredeploys.BuilderExitRuntimeCode).Length);
            Assert.Equal(69, Bytes(SystemContractPredeploys.Create2FactoryRuntimeCode).Length);
        }

        private static void AssertRuntimeCodeIsDeploymentTail(
            string deploymentTransactionInput, string constructorPrefix, string runtimeCode)
        {
            Assert.StartsWith(Hex(constructorPrefix), Hex(deploymentTransactionInput), StringComparison.Ordinal);
            Assert.Equal(Hex(deploymentTransactionInput), Hex(constructorPrefix) + Hex(runtimeCode));
        }

        private static void AssertSameAddress(string expected, string actual)
        {
            Assert.Equal(Hex(expected), Hex(actual));
        }

        private static string DeploymentTail(string deploymentTransactionInput, string constructorPrefix)
        {
            return Hex(deploymentTransactionInput).Substring(Hex(constructorPrefix).Length);
        }

        private static string Hex(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(2);
            return trimmed.ToLowerInvariant();
        }

        private static byte[] Bytes(string value)
        {
            var hex = Hex(value);
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        private static string ToHex(byte[] value)
        {
            return string.Concat(value.Select(singleByte => singleByte.ToString("x2")));
        }

        private static string FlipFirstByte(string value)
        {
            var bytes = Bytes(value);
            bytes[0] ^= 0xff;
            return ToHex(bytes);
        }
    }
}
