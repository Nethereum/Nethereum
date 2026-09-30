using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.SessionKeys;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.Example.Core
{
    /// <summary>
    /// The "policies" tab: demonstrates ERC-7715-style session keys on the session's active modular
    /// account via the SmartSession stack. <see cref="CreateScopedSessionKeyAsync"/> installs
    /// <c>SmartSession</c> as a validator (a userOp self-call, first time only - see
    /// <see cref="ModulesViewModel"/> for the same self-call pattern), generates a session key
    /// (<see cref="SessionKeyManager"/>), and enables ONE session with TWO actions - reusing
    /// <see cref="SmartSessionConfig"/>/<see cref="ActionDataBuilder"/>/<see cref="UniActionPolicyBuilder"/>,
    /// the same builders <c>SmartSessionUseCaseTests</c> exercises, enabled for real via
    /// <c>SmartSessionService.EnableSessionsRequestAndWaitForReceiptAsync</c> (a second self-call):
    /// <list type="bullet">
    /// <item><description><see cref="TestCounterService.CountRequestAsync()"/>, scoped by a <c>SudoPolicy</c>
    /// (unconditionally allowed) - <c>count()</c> takes no arguments, and UniActionPolicy's own
    /// <c>checkAction</c> requires at least one param rule AND unconditionally reads that rule's 32
    /// bytes from the call's argument data, so it cannot gate a truly zero-argument call without
    /// reverting on every real invocation (confirmed directly against
    /// contracts/lib/smartsessions/contracts/external/policies/UniActionPolicy.sol - no C#/test
    /// reference exercises this on a real deployed contract). SudoPolicy is Rhinestone's own
    /// param-less "allow" policy for exactly this shape of action.</description></item>
    /// <item><description><see cref="TestCounterService.GasWasterRequestAsync(BigInteger,string)"/>, scoped by a
    /// <c>UniActionPolicy</c> capping its <c>repeat</c> argument to <see cref="GasWasterCap"/> - a
    /// genuine parameter-level restriction UniActionPolicy CAN enforce (a real argument at a fixed
    /// calldata offset).</description></item>
    /// </list>
    /// The session key never touches the account's own owner key: it authenticates through
    /// <see cref="SmartSessionValidatorModule"/>/<see cref="SmartSessionKeySigningService"/>, a
    /// SEPARATE <see cref="NethereumSmartAccount"/> attached to the SAME address (mirrors
    /// <c>WebAuthnAAClientExtensions.GetWebAuthnAccount</c>). <see cref="UseSessionKeyAsync"/> proves the
    /// scoped key works (count() succeeds, the counter increments); <see cref="TryOutOfPolicyActionAsync"/>
    /// proves the policy actually constrains it - a gasWaster call within <see cref="GasWasterCap"/>
    /// succeeds, one beyond it is rejected by SmartSession's own validation (UniActionPolicy's
    /// <c>checkAction</c> fails the rule, which <c>PolicyLib.callPolicy</c> turns into a revert -
    /// <c>ISmartSession.PolicyViolation</c> - before the op ever lands). Reads
    /// <see cref="SessionState.SmartSession"/>/<see cref="SessionState.Counter"/>/
    /// <see cref="SessionState.PoliciesConfig"/>/<see cref="SessionState.Web3"/> lazily at
    /// command-invocation time rather than constructor-injecting them.
    /// </summary>
    public partial class PoliciesViewModel : TabViewModel
    {
        private const int GasWasterCap = 3;
        private const int WithinCapRepeat = 1;
        private const int OverCapRepeat = 100;

        private readonly SessionState _session;
        private readonly SessionKeyManager _sessionKeyManager = new SessionKeyManager();

        private NethereumSmartAccount? _sessionAccount;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private bool _hasScopedSession;

        [ObservableProperty]
        private string? _sessionKeyAddress;

        [ObservableProperty]
        private string? _policySummary;

        [ObservableProperty]
        private BigInteger _count;

        [ObservableProperty]
        private bool? _outOfPolicyRejected;

        public PoliciesViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        [RelayCommand]
        private Task CreateScopedSessionKeyAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var smartSession = _session.SmartSession!;
            var counter = _session.Counter!;
            var config = _session.PoliciesConfig!;

            var accountService = new NethereumAccountService(_session.Web3!, account.Address);
            accountService.UseAccountAbstraction(account, _session.Client!);

            var smartSessionInstalled = await accountService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, config.SmartSessionAddress, Array.Empty<byte>()).ConfigureAwait(false);

            var generated = await _sessionKeyManager.GenerateSessionKeyAsync(account.Address, validDays: 1).ConfigureAwait(false);
            var sessionKey = new EthECKey(generated.PrivateKey);

            var salt = new byte[32];
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, salt, 0, 16);
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, salt, 16, 16);

            var counterAddress = counter.ContractHandler.ContractAddress;
            var countSelector = new CountFunction().GetCallData();
            var gasWasterSelector = new GasWasterFunction { Repeat = 0, ReturnValue2 = string.Empty }.GetCallData()[..4];
            var gasWasterCapInitData = new UniActionPolicyBuilder()
                .WithValueLimit(BigInteger.Zero)
                .WithMaxValue(0, new BigInteger(GasWasterCap).ToByteArray(isUnsigned: true, isBigEndian: true))
                .Build();

            var sessionConfig = new SmartSessionConfig()
                .WithSessionValidator(config.SessionValidatorAddress)
                .WithSessionValidatorInitData(sessionKey.GetPublicAddress())
                .WithSalt(salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(counterAddress)
                    .WithSelector(countSelector)
                    .WithSudoPolicy(config.SudoPolicyAddress)
                    .Build())
                .WithAction(new ActionDataBuilder()
                    .WithTarget(counterAddress)
                    .WithSelector(gasWasterSelector)
                    .WithUniActionPolicy(config.UniActionPolicyAddress, gasWasterCapInitData)
                    .Build());

            var session = sessionConfig.ToSession();
            var permissionId = await smartSession.GetPermissionIdQueryAsync(session).ConfigureAwait(false);

            AATransactionReceipt receipt;
            if (!smartSessionInstalled)
            {
                sessionConfig.ModuleAddress = config.SmartSessionAddress;
                receipt = (AATransactionReceipt)await accountService.InstallModuleAndWaitForReceiptAsync(sessionConfig).ConfigureAwait(false);
            }
            else
            {
                smartSession.UseAccountAbstraction(account, _session.Client!);
                receipt = (AATransactionReceipt)await smartSession.EnableSessionsRequestAndWaitForReceiptAsync(
                    new List<Session> { session }).ConfigureAwait(false);
            }
            LastReceipt = receipt;

            if (!receipt.UserOpSuccess)
            {
                StatusMessage = receipt.FailureDiagnostic;
                return;
            }

            await _sessionKeyManager.MarkRegisteredAsync(generated.Key).ConfigureAwait(false);

            _sessionAccount = _session.Client!.GetAccount(
                account.Address,
                new SmartSessionKeySigningService(sessionKey, permissionId),
                new SmartSessionValidatorModule(config.SmartSessionAddress, permissionId));

            SessionKeyAddress = sessionKey.GetPublicAddress();
            PolicySummary = $"count() via SudoPolicy (open); gasWaster(repeat) via UniActionPolicy capped at repeat <= {GasWasterCap} " +
                             $"(permissionId {permissionId.ToHex(true)})";
            HasScopedSession = true;
            OutOfPolicyRejected = null;

            StatusMessage = $"Session key {SessionKeyAddress} enabled - it may ONLY call count() and gasWaster(repeat <= {GasWasterCap}) on TestCounter (userOpHash {receipt.UserOpHash})";
        });

        [RelayCommand]
        private Task UseSessionKeyAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var sessionAccount = RequireSessionAccount();
            var counter = _session.Counter!;

            counter.UseAccountAbstraction(sessionAccount, _session.Client!);

            var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync().ConfigureAwait(false);
            LastReceipt = receipt;
            Count = await counter.CountersQueryAsync(account.Address).ConfigureAwait(false);

            StatusMessage = receipt.UserOpSuccess
                ? $"Session key counted to {Count} (userOpHash {receipt.UserOpHash}) - the account's owner key never signed this"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task TryOutOfPolicyActionAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var sessionAccount = RequireSessionAccount();
            var counter = _session.Counter!;
            counter.UseAccountAbstraction(sessionAccount, _session.Client!);

            var allowedReceipt = (AATransactionReceipt)await counter.GasWasterRequestAndWaitForReceiptAsync(
                WithinCapRepeat, string.Empty).ConfigureAwait(false);
            LastReceipt = allowedReceipt;

            if (!allowedReceipt.UserOpSuccess)
            {
                OutOfPolicyRejected = null;
                StatusMessage = $"UNEXPECTED: an in-policy gasWaster({WithinCapRepeat}) call failed: {allowedReceipt.FailureDiagnostic}";
                return;
            }

            try
            {
                var overCapReceipt = (AATransactionReceipt)await counter.GasWasterRequestAndWaitForReceiptAsync(
                    OverCapRepeat, string.Empty).ConfigureAwait(false);
                LastReceipt = overCapReceipt;
                OutOfPolicyRejected = false;
                StatusMessage = $"UNEXPECTED: gasWaster({OverCapRepeat}) succeeded - the UniActionPolicy cap of {GasWasterCap} did not hold.";
            }
            catch (Exception ex)
            {
                OutOfPolicyRejected = true;
                StatusMessage = $"gasWaster({WithinCapRepeat}) succeeded (within the UniActionPolicy cap of {GasWasterCap}); " +
                                 $"gasWaster({OverCapRepeat}) was REJECTED as expected: {DescribeError(ex)}";
            }
        });

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create or attach one first via SetupViewModel.CreateAccountAsync.");

        private NethereumSmartAccount RequireSessionAccount() =>
            _sessionAccount ?? throw new InvalidOperationException(
                "No scoped session key yet - create one first via CreateScopedSessionKeyCommand.");
    }
}
