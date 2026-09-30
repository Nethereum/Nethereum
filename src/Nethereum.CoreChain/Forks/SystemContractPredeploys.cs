using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Documentation;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Forks
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "SystemContractPredeploy — one predeployed system contract and the fork that activates it")]
    public sealed class SystemContractPredeploy
    {
        public SystemContractPredeploy(string address, string runtimeCodeHex, HardforkName activationFork, string name = null)
        {
            Address = address;
            RuntimeCode = runtimeCodeHex.HexToByteArray();
            ActivationFork = activationFork;
            Name = name;
        }

        public string Address { get; }

        public byte[] RuntimeCode { get; }

        public HardforkName ActivationFork { get; }

        /// <summary>
        /// EIP-7910 <c>eth_config</c> canonical name, or null for a predeploy that no
        /// eth_config systemContracts entry names (the EIP-7997 CREATE2 factory).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// EIP-2935 §Specification / EIP-161 handling: <i>"the account at
        /// <c>HISTORY_STORAGE_ADDRESS</c> will have code and a nonce of 1, and
        /// will be exempt from EIP-161 cleanup."</i> EIP-7997 §Specification:
        /// <i>"including the account in the genesis state with a nonce equal to
        /// <c>1</c>."</i>
        /// </summary>
        public const int Nonce = 1;
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "SystemContractPredeploys — the system-contract runtime code a genesis must carry")]
    public static class SystemContractPredeploys
    {
        public const string HistoryStorageRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe14604657602036036042575f356001430381" +
            "11604257611fff81430311604257611fff9006545f5260205ff35b5f5ffd5b5f35611fff6001430306" +
            "5500";

        public const string BeaconRootsRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe14604d57602036146024575f5ffd5b5f3580" +
            "1560495762001fff810690815414603c575f5ffd5b62001fff01545f5260205ff35b5f5ffd5b62001f" +
            "ff42064281555f359062001fff015500";

        public const string Create2FactoryAddress = "0x4e59b44847b379578588920cA78FbF26c0B4956C";

        /// <summary>
        /// EIP-7997 §Specification: <i>"The chain's state MUST include
        /// <c>FACTORY_ADDRESS</c> with nonzero nonce and this runtime
        /// code"</i>.
        /// </summary>
        public const string Create2FactoryRuntimeCode =
            "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe03601600081602082378035828234f58015156039578182fd5b8082525050506014600cf3";

        public const string WithdrawalRequestsRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe1460cb5760115f54807fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff146101f457600182026001905f5b5f82111560685781019083028483029004916001019190604d565b909390049250505036603814608857366101f457346101f4575f5260205ff35b34106101f457600154600101600155600354806003026004013381556001015f35815560010160203590553360601b5f5260385f601437604c5fa0600101600355005b6003546002548082038060101160df575060105b5f5b8181146101835782810160030260040181604c02815460601b8152601401816001015481526020019060020154807fffffffffffffffffffffffffffffffff00000000000000000000000000000000168252906010019060401c908160381c81600701538160301c81600601538160281c81600501538160201c81600401538160181c81600301538160101c81600201538160081c81600101535360010160e1565b910180921461019557906002556101a0565b90505f6002555f6003555b5f54807fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff14156101cd57505f5b6001546002828201116101e25750505f6101e8565b01600290035b5f555f600155604c025ff35b5f5ffd";

        public const string ConsolidationRequestsRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe1460d35760115f54807fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff1461019a57600182026001905f5b5f82111560685781019083028483029004916001019190604d565b9093900492505050366060146088573661019a573461019a575f5260205ff35b341061019a57600154600101600155600354806004026004013381556001015f358155600101602035815560010160403590553360601b5f5260605f60143760745fa0600101600355005b6003546002548082038060021160e7575060025b5f5b8181146101295782810160040260040181607402815460601b815260140181600101548152602001816002015481526020019060030154905260010160e9565b910180921461013b5790600255610146565b90505f6002555f6003555b5f54807fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff141561017357505f5b6001546001828201116101885750505f61018e565b01600190035b5f555f6001556074025ff35b5f5ffd";

        public const string BuilderDepositRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe1461011c575f54807fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff146102705760015460088111605257506058565b60089003015b601190600182026001905f5b5f821115607f57810190830284830290049160010191906064565b90939004925050503660b814609f57366102705734610270575f5260205ff35b8034106102705760383567ffffffffffffffff1680633b9aca001161027057633b9aca00029034031061027057600154600101600155600354806006026004015f358155600101602035815560010160403581556001016060358155600101608035815560010160a035905560b85f5f3760b85fa0600101600355005b60035460025480820380604011610131575060405b5f5b8181146101d7578281016006026004018160b8028154815260200181600101548152602001816002015480825260401c67ffffffffffffffff16816010018160381c81600701538160301c81600601538160281c81600501538160201c81600401538160181c81600301538160101c81600201538160081c816001015353602001816003015481526020018160040154815260200190600501549052600101610133565b91018092146101e957906002556101f4565b90505f6002555f6003555b36610242575f54600154817fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff1461023057600882820111610238575b50505f610264565b0160089003610264565b7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff5b5f555f60015560b8025ff35b5f5ffd";

        public const string BuilderExitRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe1460e1575f54807fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff146101c65760015460028111605157506057565b60029003015b601190600182026001905f5b5f821115607e57810190830284830290049160010191906063565b909390049250505036603014609e57366101c657346101c6575f5260205ff35b34106101c657600154600101600155600354806003026004013381556001015f35815560010160203590553360601b5f5260305f60143760445fa0600101600355005b6003546002548082038060101160f5575060105b5f5b81811461012d5782810160030260040181604402815460601b8152601401816001015481526020019060020154905260010160f7565b910180921461013f579060025561014a565b90505f6002555f6003555b36610198575f54600154817fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff146101865760028282011161018e575b50505f6101ba565b01600290036101ba565b7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff5b5f555f6001556044025ff35b5f5ffd";

        /// <summary>
        /// EIP-7997 §Specification: <i>"A chain may satisfy this requirement by
        /// either of the following means that results in the account above:
        /// deploying the factory with an ordinary transaction (for example, its
        /// keyless creation transaction) where the chain's gas parameters
        /// permit it, or including the account in the genesis state with a
        /// nonce equal to 1."</i>
        /// </summary>
        public static readonly SystemContractPredeploy Create2Factory =
            new SystemContractPredeploy(
                Create2FactoryAddress, Create2FactoryRuntimeCode, HardforkName.Amsterdam);

        public static readonly IReadOnlyList<SystemContractPredeploy> All = new[]
        {
            new SystemContractPredeploy(
                Eip4788Constants.BeaconRootsAddress, BeaconRootsRuntimeCode, HardforkName.Cancun,
                name: "BEACON_ROOTS_ADDRESS"),
            new SystemContractPredeploy(
                Eip2935Constants.HistoryStorageAddress, HistoryStorageRuntimeCode, HardforkName.Prague,
                name: "HISTORY_STORAGE_ADDRESS"),

            new SystemContractPredeploy(
                SystemCallContracts.WithdrawalRequests, WithdrawalRequestsRuntimeCode, HardforkName.Prague,
                name: "WITHDRAWAL_REQUEST_PREDEPLOY_ADDRESS"),
            new SystemContractPredeploy(
                SystemCallContracts.ConsolidationRequests, ConsolidationRequestsRuntimeCode, HardforkName.Prague,
                name: "CONSOLIDATION_REQUEST_PREDEPLOY_ADDRESS"),

            new SystemContractPredeploy(
                SystemCallContracts.BuilderDeposit, BuilderDepositRuntimeCode, HardforkName.Amsterdam,
                name: "BUILDER_DEPOSIT_CONTRACT_ADDRESS"),
            new SystemContractPredeploy(
                SystemCallContracts.BuilderExit, BuilderExitRuntimeCode, HardforkName.Amsterdam,
                name: "BUILDER_EXIT_CONTRACT_ADDRESS"),

            Create2Factory
        };

        public static IEnumerable<SystemContractPredeploy> For(HardforkName fork)
        {
            foreach (var predeploy in All)
            {
                if (fork >= predeploy.ActivationFork) yield return predeploy;
            }
        }

        public static Task ApplyGenesisAllocationAsync(IStateStore stateStore, HardforkName fork) =>
            ApplyGenesisAllocationAsync(stateStore, fork, All);

        public static async Task ApplyGenesisAllocationAsync(
            IStateStore stateStore, HardforkName fork, IEnumerable<SystemContractPredeploy> predeploys)
        {
            if (stateStore == null) throw new ArgumentNullException(nameof(stateStore));
            if (predeploys == null) throw new ArgumentNullException(nameof(predeploys));

            foreach (var predeploy in predeploys)
            {
                if (fork < predeploy.ActivationFork) continue;
                await AllocateIfAbsentAsync(stateStore, predeploy).ConfigureAwait(false);
            }
        }

        public static async Task AllocateIfAbsentAsync(IStateStore stateStore, SystemContractPredeploy predeploy)
        {
            if (stateStore == null) throw new ArgumentNullException(nameof(stateStore));
            if (predeploy == null) throw new ArgumentNullException(nameof(predeploy));

            var existing = await stateStore.GetAccountAsync(predeploy.Address).ConfigureAwait(false);
            if (HasCode(existing)) return;

            var codeHash = Sha3Keccack.Current.CalculateHash(predeploy.RuntimeCode);
            await stateStore.SaveCodeAsync(codeHash, predeploy.RuntimeCode).ConfigureAwait(false);
            await stateStore.SaveAccountAsync(predeploy.Address, new Account
            {
                Balance = existing?.Balance ?? EvmUInt256.Zero,
                Nonce = SystemContractPredeploy.Nonce,
                CodeHash = codeHash
            }).ConfigureAwait(false);
        }

        private static bool HasCode(Account account)
        {
            if (account?.CodeHash == null) return false;
            return !ByteUtil.AreEqual(account.CodeHash, DefaultValues.EMPTY_DATA_HASH);
        }
    }
}
