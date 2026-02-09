using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Governance
{
    public class FactoryGovernanceSigner
    {
        private readonly string _domainName;
        private readonly string _domainVersion;
        private readonly BigInteger _chainId;
        private readonly string _verifyingContract;

        public FactoryGovernanceSigner(BigInteger chainId, string verifyingContract)
        {
            _domainName = "SmartAccountFactoryGovernance";
            _domainVersion = "1";
            _chainId = chainId;
            _verifyingContract = verifyingContract;
        }

        public Domain GetDomain()
        {
            return new Domain
            {
                Name = _domainName,
                Version = _domainVersion,
                ChainId = _chainId,
                VerifyingContract = _verifyingContract
            };
        }

        public TypedData<Domain> GetTypedDefinition<T>()
        {
            return new TypedData<Domain>
            {
                Domain = GetDomain(),
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(T)),
                PrimaryType = typeof(T).Name,
            };
        }

        public string SignRegisterModule(RegisterModuleMessage message, string privateKey)
        {
            return SignMessage(message, privateKey);
        }

        public string SignUnregisterModule(UnregisterModuleMessage message, string privateKey)
        {
            return SignMessage(message, privateKey);
        }

        public string SignUpdateAdmins(UpdateAdminsMessage message, string privateKey)
        {
            return SignMessage(message, privateKey);
        }

        public async Task<string> SignRegisterModuleAsync(RegisterModuleMessage message, IWeb3 web3)
        {
            return await SignTypedDataV4Async(message, web3);
        }

        public async Task<string> SignUnregisterModuleAsync(UnregisterModuleMessage message, IWeb3 web3)
        {
            return await SignTypedDataV4Async(message, web3);
        }

        public async Task<string> SignUpdateAdminsAsync(UpdateAdminsMessage message, IWeb3 web3)
        {
            return await SignTypedDataV4Async(message, web3);
        }

        private string SignMessage<T>(T message, string privateKey)
        {
            var typedData = GetTypedDefinition<T>();
            typedData.SetMessage(message);
            var signer = new Eip712TypedDataSigner();
            return signer.SignTypedDataV4(typedData, new EthECKey(privateKey));
        }

        private async Task<string> SignTypedDataV4Async<T>(T message, IWeb3 web3)
        {
            var typedData = GetTypedDefinition<T>();
            typedData.SetMessage(message);
            var json = typedData.ToJson();
            return await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(json);
        }

        public byte[] GetMessageHash<T>(T message)
        {
            var typedData = GetTypedDefinition<T>();
            typedData.SetMessage(message);
            var encoder = new Eip712TypedDataEncoder();
            return encoder.EncodeAndHashTypedData(typedData);
        }

        public bool VerifySignature<T>(T message, string signature, string expectedAddress)
        {
            var typedData = GetTypedDefinition<T>();
            typedData.SetMessage(message);
            var signer = new Eip712TypedDataSigner();
            var recoveredAddress = signer.RecoverFromSignatureV4(typedData, signature);
            return recoveredAddress.IsTheSameAddress(expectedAddress);
        }

        public string RecoverSigner<T>(T message, string signature)
        {
            var typedData = GetTypedDefinition<T>();
            typedData.SetMessage(message);
            var signer = new Eip712TypedDataSigner();
            return signer.RecoverFromSignatureV4(typedData, signature);
        }
    }

    [Struct("RegisterModule")]
    public class RegisterModuleMessage
    {
        [Parameter("bytes32", "moduleId", 1)]
        public byte[] ModuleId { get; set; } = null!;

        [Parameter("address", "moduleAddress", 2)]
        public string ModuleAddress { get; set; } = null!;

        [Parameter("uint256", "nonce", 3)]
        public BigInteger Nonce { get; set; }

        [Parameter("uint256", "deadline", 4)]
        public BigInteger Deadline { get; set; }
    }

    [Struct("UnregisterModule")]
    public class UnregisterModuleMessage
    {
        [Parameter("bytes32", "moduleId", 1)]
        public byte[] ModuleId { get; set; } = null!;

        [Parameter("uint256", "nonce", 2)]
        public BigInteger Nonce { get; set; }

        [Parameter("uint256", "deadline", 3)]
        public BigInteger Deadline { get; set; }
    }

    [Struct("UpdateAdmins")]
    public class UpdateAdminsMessage
    {
        [Parameter("address[]", "newAdmins", 1)]
        public List<string> NewAdmins { get; set; } = null!;

        [Parameter("uint256", "newThreshold", 2)]
        public BigInteger NewThreshold { get; set; }

        [Parameter("uint256", "nonce", 3)]
        public BigInteger Nonce { get; set; }

        [Parameter("uint256", "deadline", 4)]
        public BigInteger Deadline { get; set; }
    }
}
