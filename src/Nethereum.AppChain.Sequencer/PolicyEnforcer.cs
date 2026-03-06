using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.AppChain.Sequencer
{
    public class PolicyEnforcer : IPolicyEnforcer
    {
        private PolicyConfig _policy;
        private HashSet<string> _allowedWritersSet;
        private readonly IAppChain _appChain;
        private readonly object _lock = new();

        public PolicyConfig Policy => _policy;

        public PolicyEnforcer(PolicyConfig policy, IAppChain appChain)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _appChain = appChain ?? throw new ArgumentNullException(nameof(appChain));
            _allowedWritersSet = BuildAllowedWritersSet(policy.AllowedWriters);
        }

        public Task<PolicyValidationResult> ValidateTransactionAsync(ISignedTransaction transaction)
        {
            PolicyConfig policy;
            HashSet<string> allowedWritersSet;

            lock (_lock)
            {
                policy = _policy;
                allowedWritersSet = _allowedWritersSet;
            }

            if (!policy.Enabled)
            {
                return Task.FromResult(PolicyValidationResult.Valid());
            }

            var sender = RecoverSenderAddress(transaction);
            if (string.IsNullOrEmpty(sender))
            {
                return Task.FromResult(PolicyValidationResult.Invalid(
                    PolicyViolationType.InvalidSignature,
                    "Could not recover sender address from signature"));
            }

            if (allowedWritersSet.Count > 0)
            {
                var normalizedSender = NormalizeAddress(sender);
                if (!allowedWritersSet.Contains(normalizedSender))
                {
                    return Task.FromResult(PolicyValidationResult.Invalid(
                        PolicyViolationType.UnauthorizedSender,
                        $"Sender {sender} is not in the allowed writers list"));
                }
            }

            var rlpEncodedSize = transaction.GetRLPEncodedRaw()?.Length ?? 0;
            if (rlpEncodedSize > policy.MaxCalldataBytes)
            {
                return Task.FromResult(PolicyValidationResult.Invalid(
                    PolicyViolationType.CalldataTooLarge,
                    $"Transaction size {rlpEncodedSize} exceeds maximum {policy.MaxCalldataBytes}"));
            }

            return Task.FromResult(PolicyValidationResult.Valid());
        }

        public void UpdatePolicy(PolicyConfig policy)
        {
            lock (_lock)
            {
                _policy = policy ?? throw new ArgumentNullException(nameof(policy));
                _allowedWritersSet = BuildAllowedWritersSet(policy.AllowedWriters);
            }
        }

        public void UpdateWritersRoot(byte[] writersRoot)
        {
            lock (_lock)
            {
                _policy.WritersRoot = writersRoot;
            }
        }

        private static HashSet<string> BuildAllowedWritersSet(List<string>? allowedWriters)
        {
            if (allowedWriters == null || allowedWriters.Count == 0)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                allowedWriters.Select(NormalizeAddress),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeAddress(string address)
        {
            return address?.ToLowerInvariant().Replace("0x", "") ?? "";
        }

        private static string? RecoverSenderAddress(ISignedTransaction transaction)
        {
            try
            {
                if (transaction.Signature == null)
                    return null;

                return transaction.GetEthECKey()?.GetPublicAddress();
            }
            catch
            {
                return null;
            }
        }
    }
}
