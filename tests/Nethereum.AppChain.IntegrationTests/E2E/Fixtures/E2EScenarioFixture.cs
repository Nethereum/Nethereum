using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.Builder;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Xunit;
using ISignedTransaction = Nethereum.Model.ISignedTransaction;
using Transaction1559 = Nethereum.Model.Transaction1559;
using Signature = Nethereum.Model.Signature;

namespace Nethereum.AppChain.IntegrationTests.E2E.Fixtures
{
    public abstract class E2EScenarioFixture : IAsyncLifetime
    {
        public const int DEFAULT_CHAIN_ID = 420420;
        public const string OPERATOR_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        public AppChainInstance? Chain { get; set; }
        public Account OperatorAccount { get; private set; } = null!;
        public Account[] TestAccounts { get; private set; } = null!;

        public virtual async Task InitializeAsync()
        {
            OperatorAccount = new Account(OPERATOR_PRIVATE_KEY, DEFAULT_CHAIN_ID);
            TestAccounts = CreateTestAccounts(10);
            await Task.CompletedTask;
        }

        public virtual async Task DisposeAsync()
        {
            if (Chain != null)
                await Chain.DisposeAsync();
        }

        private static int _accountCounter = 100;

        public Account[] CreateTestAccounts(int count)
        {
            return Enumerable.Range(0, count)
                .Select(_ =>
                {
                    var counter = System.Threading.Interlocked.Increment(ref _accountCounter);
                    var key = new byte[32];
                    key[28] = (byte)(counter >> 24);
                    key[29] = (byte)(counter >> 16);
                    key[30] = (byte)(counter >> 8);
                    key[31] = (byte)counter;
                    return new Account("0x" + key.ToHex(), DEFAULT_CHAIN_ID);
                })
                .ToArray();
        }

        public string[] GetTestAddresses()
        {
            return TestAccounts.Select(a => a.Address).ToArray();
        }

        public string[] GetAllAddresses()
        {
            var addresses = new List<string> { OperatorAccount.Address };
            addresses.AddRange(GetTestAddresses());
            return addresses.ToArray();
        }

        public IWeb3 GetWeb3(Account account)
        {
            var web3 = new Web3.Web3(account, Chain!.RpcClient);
            web3.TransactionManager.UseLegacyAsDefault = true;
            return web3;
        }

        public IWeb3 GetOperatorWeb3()
        {
            return GetWeb3(OperatorAccount);
        }

        public async Task<(string TxHash, bool Success)> SendTransactionAsync(Account from, string to, BigInteger value)
        {
            try
            {
                var nonce = await Chain!.AppChain.GetNonceAsync(from.Address);
                var tx = CreateSignedTransaction(from, to, value, nonce);
                var txHash = await Chain.Sequencer.SubmitTransactionAsync(tx);
                return ("0x" + txHash.ToHex(), true);
            }
            catch (Exception)
            {
                return (string.Empty, false);
            }
        }

        public async Task<(string TxHash, bool Success, string? Error)> TrySendTransactionAsync(Account from, string to, BigInteger value)
        {
            try
            {
                var nonce = await Chain!.AppChain.GetNonceAsync(from.Address);
                var tx = CreateSignedTransaction(from, to, value, nonce);
                var txHash = await Chain.Sequencer.SubmitTransactionAsync(tx);
                return ("0x" + txHash.ToHex(), true, null);
            }
            catch (Exception ex)
            {
                return (string.Empty, false, ex.Message);
            }
        }

        public ISignedTransaction CreateSignedTransaction(Account from, string to, BigInteger value, BigInteger nonce, string? data = null)
        {
            var privateKey = new EthECKey(from.PrivateKey);
            var tx = new Transaction1559(
                chainId: DEFAULT_CHAIN_ID,
                nonce: nonce,
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(2_000_000_000),
                gasLimit: new BigInteger(!string.IsNullOrEmpty(data) ? 100000 : 21000),
                receiverAddress: to,
                amount: value,
                data: data,
                accessList: null);

            var sig = privateKey.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = sig.R, S = sig.S, V = sig.V });
            return tx;
        }

        public async Task ProduceBlockAsync()
        {
            await Chain!.ProduceBlockAsync();
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            return await Chain!.GetBalanceAsync(address);
        }

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            return await Chain!.GetBlockNumberAsync();
        }
    }

    public class OpenTrustFixture : E2EScenarioFixture
    {
        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            Chain = await AppChainPresets
                .ForGaming("OpenGameChain", DEFAULT_CHAIN_ID, OPERATOR_PRIVATE_KEY)
                .WithPrefundedAddresses(GetTestAddresses())
                .BuildAsync();
        }
    }

    public class WhitelistTrustFixture : E2EScenarioFixture
    {
        private List<string> _currentWhitelist = new();

        public IReadOnlyList<string> CurrentWhitelist => _currentWhitelist;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            _currentWhitelist = new List<string> { OperatorAccount.Address.ToLower() };

            Chain = await AppChainPresets
                .ForEnterprise("CorpChain", DEFAULT_CHAIN_ID, OPERATOR_PRIVATE_KEY, OperatorAccount.Address)
                .WithPrefundedAddresses(GetTestAddresses())
                .BuildAsync();

            UpdatePolicy();
        }

        public void AddToWhitelist(string address)
        {
            if (!_currentWhitelist.Contains(address.ToLower()))
            {
                _currentWhitelist.Add(address.ToLower());
            }
            UpdatePolicy();
        }

        public void RemoveFromWhitelist(string address)
        {
            _currentWhitelist.Remove(address.ToLower());
            UpdatePolicy();
        }

        public bool IsWhitelisted(string address)
        {
            return _currentWhitelist.Contains(address.ToLower());
        }

        private void UpdatePolicy()
        {
            Chain!.Sequencer.PolicyEnforcer.UpdatePolicy(
                PolicyConfig.RestrictedAccess(_currentWhitelist));
        }
    }

    public class InviteTreeFixture : E2EScenarioFixture
    {
        private const int MAX_INVITES = 3;
        private readonly Dictionary<string, List<string>> _inviteTree = new();
        private readonly HashSet<string> _activatedUsers = new();

        public IReadOnlyCollection<string> ActivatedUsers => _activatedUsers;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            Chain = await AppChainPresets
                .ForSocial("SocialChain", DEFAULT_CHAIN_ID, OPERATOR_PRIVATE_KEY, maxInvites: MAX_INVITES)
                .WithPrefundedAddresses(GetTestAddresses())
                .BuildAsync();

            var rootAddress = OperatorAccount.Address.ToLower();
            _activatedUsers.Add(rootAddress);
            _inviteTree[rootAddress] = new List<string>();

            UpdatePolicy();
        }

        public bool InviteUser(string inviter, string invitee)
        {
            var inviterNorm = inviter.ToLower();
            var inviteeNorm = invitee.ToLower();

            if (!_activatedUsers.Contains(inviterNorm))
                return false;

            if (!_inviteTree.ContainsKey(inviterNorm))
                _inviteTree[inviterNorm] = new List<string>();

            if (_inviteTree[inviterNorm].Count >= MAX_INVITES)
                return false;

            if (_activatedUsers.Contains(inviteeNorm))
                return false;

            _inviteTree[inviterNorm].Add(inviteeNorm);
            _activatedUsers.Add(inviteeNorm);
            _inviteTree[inviteeNorm] = new List<string>();

            UpdatePolicy();
            return true;
        }

        public int GetInviteCount(string address)
        {
            var norm = address.ToLower();
            return _inviteTree.TryGetValue(norm, out var invites) ? invites.Count : 0;
        }

        public int RemainingInvites(string address)
        {
            return MAX_INVITES - GetInviteCount(address);
        }

        public bool IsActivated(string address)
        {
            return _activatedUsers.Contains(address.ToLower());
        }

        private void UpdatePolicy()
        {
            Chain!.Sequencer.PolicyEnforcer.UpdatePolicy(
                PolicyConfig.RestrictedAccess(_activatedUsers.ToList()));
        }
    }
}
