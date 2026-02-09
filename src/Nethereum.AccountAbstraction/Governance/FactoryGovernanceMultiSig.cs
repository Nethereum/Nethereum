using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Governance
{
    public class FactoryGovernanceMultiSig
    {
        private readonly FactoryGovernanceSigner _signer;
        private readonly string _factoryAddress;
        private readonly BigInteger _chainId;

        public FactoryGovernanceMultiSig(BigInteger chainId, string factoryAddress)
        {
            _chainId = chainId;
            _factoryAddress = factoryAddress;
            _signer = new FactoryGovernanceSigner(chainId, factoryAddress);
        }

        public string FactoryAddress => _factoryAddress;
        public BigInteger ChainId => _chainId;

        public RegisterModuleProposal CreateRegisterModuleProposal(
            byte[] moduleId,
            string moduleAddress,
            BigInteger nonce,
            BigInteger deadline)
        {
            var message = new RegisterModuleMessage
            {
                ModuleId = moduleId,
                ModuleAddress = moduleAddress,
                Nonce = nonce,
                Deadline = deadline
            };

            return new RegisterModuleProposal
            {
                Message = message,
                MessageHash = _signer.GetMessageHash(message),
                Signatures = new List<byte[]>()
            };
        }

        public UnregisterModuleProposal CreateUnregisterModuleProposal(
            byte[] moduleId,
            BigInteger nonce,
            BigInteger deadline)
        {
            var message = new UnregisterModuleMessage
            {
                ModuleId = moduleId,
                Nonce = nonce,
                Deadline = deadline
            };

            return new UnregisterModuleProposal
            {
                Message = message,
                MessageHash = _signer.GetMessageHash(message),
                Signatures = new List<byte[]>()
            };
        }

        public UpdateAdminsProposal CreateUpdateAdminsProposal(
            List<string> newAdmins,
            BigInteger newThreshold,
            BigInteger nonce,
            BigInteger deadline)
        {
            var message = new UpdateAdminsMessage
            {
                NewAdmins = newAdmins,
                NewThreshold = newThreshold,
                Nonce = nonce,
                Deadline = deadline
            };

            return new UpdateAdminsProposal
            {
                Message = message,
                MessageHash = _signer.GetMessageHash(message),
                Signatures = new List<byte[]>()
            };
        }

        public string SignRegisterModule(RegisterModuleProposal proposal, string privateKey)
        {
            var signature = _signer.SignRegisterModule(proposal.Message, privateKey);
            proposal.Signatures.Add(signature.HexToByteArray());
            return signature;
        }

        public string SignUnregisterModule(UnregisterModuleProposal proposal, string privateKey)
        {
            var signature = _signer.SignUnregisterModule(proposal.Message, privateKey);
            proposal.Signatures.Add(signature.HexToByteArray());
            return signature;
        }

        public string SignUpdateAdmins(UpdateAdminsProposal proposal, string privateKey)
        {
            var signature = _signer.SignUpdateAdmins(proposal.Message, privateKey);
            proposal.Signatures.Add(signature.HexToByteArray());
            return signature;
        }

        public bool VerifyRegisterModuleSignature(RegisterModuleProposal proposal, string signature, string expectedAddress)
        {
            return _signer.VerifySignature(proposal.Message, signature, expectedAddress);
        }

        public bool VerifyUnregisterModuleSignature(UnregisterModuleProposal proposal, string signature, string expectedAddress)
        {
            return _signer.VerifySignature(proposal.Message, signature, expectedAddress);
        }

        public bool VerifyUpdateAdminsSignature(UpdateAdminsProposal proposal, string signature, string expectedAddress)
        {
            return _signer.VerifySignature(proposal.Message, signature, expectedAddress);
        }

        public string RecoverRegisterModuleSigner(RegisterModuleProposal proposal, string signature)
        {
            return _signer.RecoverSigner(proposal.Message, signature);
        }

        public string RecoverUnregisterModuleSigner(UnregisterModuleProposal proposal, string signature)
        {
            return _signer.RecoverSigner(proposal.Message, signature);
        }

        public string RecoverUpdateAdminsSigner(UpdateAdminsProposal proposal, string signature)
        {
            return _signer.RecoverSigner(proposal.Message, signature);
        }

        public bool HasSufficientSignatures<T>(T proposal, int threshold) where T : IMultiSigProposal
        {
            return proposal.Signatures.Count >= threshold;
        }

        public List<string> GetUniqueSigners<T>(T proposal) where T : IMultiSigProposal
        {
            var signers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var messageHash = proposal.MessageHash;

            foreach (var signature in proposal.Signatures)
            {
                try
                {
                    var r = new Org.BouncyCastle.Math.BigInteger(1, signature.Take(32).ToArray());
                    var s = new Org.BouncyCastle.Math.BigInteger(1, signature.Skip(32).Take(32).ToArray());
                    var v = new byte[] { signature[64] };
                    var ecKey = new EthECDSASignature(r, s, v);
                    var signer = EthECKey.RecoverFromSignature(ecKey, messageHash).GetPublicAddress();
                    signers.Add(signer);
                }
                catch
                {
                    // Skip invalid signatures
                }
            }

            return signers.ToList();
        }
    }

    public interface IMultiSigProposal
    {
        byte[] MessageHash { get; }
        List<byte[]> Signatures { get; }
    }

    public class RegisterModuleProposal : IMultiSigProposal
    {
        public RegisterModuleMessage Message { get; set; } = null!;
        public byte[] MessageHash { get; set; } = null!;
        public List<byte[]> Signatures { get; set; } = new();
    }

    public class UnregisterModuleProposal : IMultiSigProposal
    {
        public UnregisterModuleMessage Message { get; set; } = null!;
        public byte[] MessageHash { get; set; } = null!;
        public List<byte[]> Signatures { get; set; } = new();
    }

    public class UpdateAdminsProposal : IMultiSigProposal
    {
        public UpdateAdminsMessage Message { get; set; } = null!;
        public byte[] MessageHash { get; set; } = null!;
        public List<byte[]> Signatures { get; set; } = new();
    }
}
