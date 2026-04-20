using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Execution.Create;
using Nethereum.EVM.Execution.Create.Rules;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Execution.TransactionSetup;
using Nethereum.EVM.Execution.TransactionValidation;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Gas;

namespace Nethereum.EVM
{
    public class HardforkConfig
    {
        public int MaxBlobsPerBlock { get; set; }
        public IntrinsicGasRules IntrinsicGasRules { get; set; }
        public PrecompileRegistry Precompiles { get; set; }
        public OpcodeHandlerTable OpcodeHandlers { get; set; }
        public CallFrameInitRules CallFrameInitRules { get; set; }
        public TransactionValidationRules TransactionValidationRules { get; set; }
        public TransactionSetupRules TransactionSetupRules { get; set; }

        // CREATE code deposit: Frontier allows empty-code deploy on OOG, Homestead+ reverts
        public ICodeDepositRule CodeDepositRule { get; set; } = HomesteadCodeDepositRule.Instance;

        public IGasForwardingCalculator GasForwarding { get; set; } = Eip150GasForwarding.Instance;

        // CREATE validation: per-fork limits
        public int MaxCodeSize { get; set; } = 24576;     // EIP-170 (Spurious Dragon+). 0 = no limit (Frontier-Tangerine)
        public int MaxInitcodeSize { get; set; } = 49152;  // EIP-3860 (Shanghai+). 0 = no limit (pre-Shanghai)
        public bool RejectEfPrefix { get; set; } = true;   // EIP-3541 (London+). false = allow 0xEF deployed code
        public ulong ContractInitialNonce { get; set; } = 1;  // EIP-161 (Spurious Dragon+). 0 = Frontier-Tangerine

        // Per-fork refund constants
        public int RefundQuotient { get; set; } = 5;
        public long SstoreClearsSchedule { get; set; } = 4800;

        // EIP-161 (Spurious Dragon+): empty accounts removed from state root
        // Pre-Spurious Dragon: empty accounts remain in state root
        public bool CleanEmptyAccounts { get; set; } = true;

        private static HardforkConfig Build(
            IntrinsicGasRules intrinsic,
            OpcodeHandlerTable handlers,
            CallFrameInitRules callFrame = null,
            TransactionValidationRules validation = null,
            TransactionSetupRules setup = null,
            ICodeDepositRule codeDepositRule = null,
            IGasForwardingCalculator gasForwarding = null,
            int maxBlobs = 0,
            int maxCodeSize = 24576,
            int maxInitcodeSize = 49152,
            bool rejectEfPrefix = true,
            ulong contractInitialNonce = 1,
            int refundQuotient = 5,
            long sstoreClearsSchedule = 4800,
            bool cleanEmptyAccounts = true)
        {
            return new HardforkConfig
            {
                MaxBlobsPerBlock = maxBlobs,
                GasForwarding = gasForwarding ?? Eip150GasForwarding.Instance,
                MaxCodeSize = maxCodeSize,
                MaxInitcodeSize = maxInitcodeSize,
                RejectEfPrefix = rejectEfPrefix,
                ContractInitialNonce = contractInitialNonce,
                RefundQuotient = refundQuotient,
                SstoreClearsSchedule = sstoreClearsSchedule,
                CleanEmptyAccounts = cleanEmptyAccounts,
                CodeDepositRule = codeDepositRule ?? HomesteadCodeDepositRule.Instance,
                IntrinsicGasRules = intrinsic,
                OpcodeHandlers = handlers.Freeze(),
                CallFrameInitRules = callFrame ?? CallFrameInitRules.Empty,
                TransactionValidationRules = validation ?? TransactionValidationRules.Empty,
                TransactionSetupRules = setup ?? TransactionSetupRules.Empty,
            };
        }

        private static readonly HardforkConfig _frontier = Build(
            IntrinsicGasRuleSets.Frontier, OpcodeHandlerSets.Frontier(),
            codeDepositRule: FrontierCodeDepositRule.Instance,
            gasForwarding: FullGasForwarding.Instance, maxCodeSize: 0, maxInitcodeSize: 0, rejectEfPrefix: false, contractInitialNonce: 0,
            refundQuotient: 2, sstoreClearsSchedule: 15000, cleanEmptyAccounts: false);
        private static readonly HardforkConfig _homestead = Build(
            IntrinsicGasRuleSets.Homestead, OpcodeHandlerSets.Homestead(),
            gasForwarding: FullGasForwarding.Instance, maxCodeSize: 0, maxInitcodeSize: 0, rejectEfPrefix: false, contractInitialNonce: 0,
            refundQuotient: 2, sstoreClearsSchedule: 15000, cleanEmptyAccounts: false);
        private static readonly HardforkConfig _tangerineWhistle = Build(
            IntrinsicGasRuleSets.TangerineWhistle, OpcodeHandlerSets.TangerineWhistle(),
            maxCodeSize: 0, maxInitcodeSize: 0, rejectEfPrefix: false, contractInitialNonce: 0,
            refundQuotient: 2, sstoreClearsSchedule: 15000, cleanEmptyAccounts: false);
        private static readonly HardforkConfig _spuriousDragon = Build(
            IntrinsicGasRuleSets.SpuriousDragon, OpcodeHandlerSets.SpuriousDragon(),
            maxInitcodeSize: 0, rejectEfPrefix: false,
            refundQuotient: 2, sstoreClearsSchedule: 15000);
        private static readonly HardforkConfig _byzantium = Build(
            IntrinsicGasRuleSets.Byzantium, OpcodeHandlerSets.Byzantium(),
            maxInitcodeSize: 0, rejectEfPrefix: false,
            refundQuotient: 2, sstoreClearsSchedule: 15000);
        private static readonly HardforkConfig _constantinople = Build(
            IntrinsicGasRuleSets.Constantinople, OpcodeHandlerSets.Constantinople(),
            maxInitcodeSize: 0, rejectEfPrefix: false,
            refundQuotient: 2, sstoreClearsSchedule: 15000);
        private static readonly HardforkConfig _petersburg = Build(
            IntrinsicGasRuleSets.Petersburg, OpcodeHandlerSets.Petersburg(),
            maxInitcodeSize: 0, rejectEfPrefix: false,
            refundQuotient: 2, sstoreClearsSchedule: 15000);
        private static readonly HardforkConfig _istanbul = Build(
            IntrinsicGasRuleSets.Istanbul, OpcodeHandlerSets.Istanbul(),
            maxInitcodeSize: 0, rejectEfPrefix: false,
            refundQuotient: 2, sstoreClearsSchedule: 15000);
        private static readonly HardforkConfig _berlin = Build(
            IntrinsicGasRuleSets.Berlin, OpcodeHandlerSets.Berlin(),
            maxInitcodeSize: 0, rejectEfPrefix: false,
            refundQuotient: 2, sstoreClearsSchedule: 15000);
        private static readonly HardforkConfig _london = Build(
            IntrinsicGasRuleSets.London, OpcodeHandlerSets.London(),
            maxInitcodeSize: 0);
        private static readonly HardforkConfig _paris = Build(
            IntrinsicGasRuleSets.Paris, OpcodeHandlerSets.Paris(),
            maxInitcodeSize: 0);
        private static readonly HardforkConfig _shanghai = Build(
            IntrinsicGasRuleSets.Shanghai, OpcodeHandlerSets.Shanghai());

        private static readonly HardforkConfig _cancun = Build(
            IntrinsicGasRuleSets.Cancun, OpcodeHandlerSets.Cancun(),
            CallFrameInitRuleSets.Cancun, TransactionValidationRuleSets.Cancun, TransactionSetupRuleSets.Cancun,
            maxBlobs: 6);

        private static readonly HardforkConfig _prague = Build(
            IntrinsicGasRuleSets.Prague, OpcodeHandlerSets.Prague(),
            CallFrameInitRuleSets.Prague, TransactionValidationRuleSets.Prague, TransactionSetupRuleSets.Prague,
            maxBlobs: 9);

        private static readonly HardforkConfig _osaka = Build(
            IntrinsicGasRuleSets.Osaka, OpcodeHandlerSets.Osaka(),
            CallFrameInitRuleSets.Osaka, TransactionValidationRuleSets.Osaka, TransactionSetupRuleSets.Osaka,
            maxBlobs: 9);

        public static HardforkConfig Frontier => _frontier;
        public static HardforkConfig Homestead => _homestead;
        public static HardforkConfig TangerineWhistle => _tangerineWhistle;
        public static HardforkConfig SpuriousDragon => _spuriousDragon;
        public static HardforkConfig Byzantium => _byzantium;
        public static HardforkConfig Constantinople => _constantinople;
        public static HardforkConfig Petersburg => _petersburg;
        public static HardforkConfig Istanbul => _istanbul;
        public static HardforkConfig Berlin => _berlin;
        public static HardforkConfig London => _london;
        public static HardforkConfig Paris => _paris;
        public static HardforkConfig Shanghai => _shanghai;
        public static HardforkConfig Cancun => _cancun;
        public static HardforkConfig Prague => _prague;
        public static HardforkConfig Osaka => _osaka;
    }
}
