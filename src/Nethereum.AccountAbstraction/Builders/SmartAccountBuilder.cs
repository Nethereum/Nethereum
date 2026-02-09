using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.Encoders;
using Nethereum.AccountAbstraction.Services;
using Nethereum.Signer;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Builders
{
    public class SmartAccountBuilder
    {
        private static readonly IntTypeEncoder SaltEncoder = new IntTypeEncoder(false, 256);

        private readonly IWeb3 _web3;
        private string? _factoryAddress;
        private string? _ownerAddress;
        private EthECKey? _ownerKey;
        private byte[] _salt = new byte[32];
        private byte[]? _initData;
        private string? _existingAccountAddress;
        private string? _validatorAddress;
        private readonly List<(BigInteger moduleTypeId, string moduleAddress, byte[] initData)> _modules = new();

        public SmartAccountBuilder(IWeb3 web3)
        {
            _web3 = web3;
        }

        public SmartAccountBuilder WithFactory(string factoryAddress)
        {
            _factoryAddress = factoryAddress;
            return this;
        }

        public SmartAccountBuilder WithOwner(string ownerAddress)
        {
            _ownerAddress = ownerAddress;
            return this;
        }

        public SmartAccountBuilder WithOwnerKey(EthECKey ownerKey)
        {
            _ownerKey = ownerKey;
            _ownerAddress = ownerKey.GetPublicAddress();
            return this;
        }

        public SmartAccountBuilder WithOwnerKey(string privateKey)
        {
            return WithOwnerKey(new EthECKey(privateKey));
        }

        public SmartAccountBuilder WithSalt(byte[] salt)
        {
            if (salt.Length != 32)
                throw new ArgumentException("Salt must be 32 bytes");
            _salt = salt;
            return this;
        }

        public SmartAccountBuilder WithSalt(BigInteger salt)
        {
            _salt = SaltEncoder.EncodeInt(salt);
            return this;
        }

        public SmartAccountBuilder WithInitData(byte[] initData)
        {
            _initData = initData;
            return this;
        }

        public SmartAccountBuilder WithValidator(string validatorAddress)
        {
            _validatorAddress = validatorAddress;
            return this;
        }

        public SmartAccountBuilder WithModule(BigInteger moduleTypeId, string moduleAddress, byte[] initData)
        {
            _modules.Add((moduleTypeId, moduleAddress, initData));
            return this;
        }

        public SmartAccountBuilder FromExisting(string accountAddress)
        {
            _existingAccountAddress = accountAddress;
            return this;
        }

        public async Task<SmartAccountService> BuildAsync()
        {
            if (!string.IsNullOrEmpty(_existingAccountAddress))
            {
                return await SmartAccountService.LoadAsync(_web3, _existingAccountAddress);
            }

            if (string.IsNullOrEmpty(_factoryAddress))
                throw new InvalidOperationException("Factory address is required. Use WithFactory().");

            var initData = GetInitData();
            var factory = await SmartAccountFactoryService.LoadAsync(_web3, _factoryAddress);

            return await SmartAccountService.CreateAsync(_web3, factory, _salt, initData);
        }

        public async Task<string> GetAddressAsync()
        {
            if (!string.IsNullOrEmpty(_existingAccountAddress))
                return _existingAccountAddress;

            if (string.IsNullOrEmpty(_factoryAddress))
                throw new InvalidOperationException("Factory address is required. Use WithFactory().");

            var initData = GetInitData();
            var factory = await SmartAccountFactoryService.LoadAsync(_web3, _factoryAddress);

            return await factory.GetAccountAddressAsync(_salt, initData);
        }

        public async Task<byte[]> GetInitCodeAsync()
        {
            if (string.IsNullOrEmpty(_factoryAddress))
                throw new InvalidOperationException("Factory address is required. Use WithFactory().");

            var initData = GetInitData();
            var factory = new SmartAccountFactoryService(_web3, _factoryAddress);

            return await factory.GetInitCodeAsync(_salt, initData);
        }

        private byte[] GetInitData()
        {
            if (_initData != null)
                return _initData;

            if (string.IsNullOrEmpty(_ownerAddress) && string.IsNullOrEmpty(_validatorAddress))
                throw new InvalidOperationException("Owner or validator address is required.");

            return EncodeInitData();
        }

        private byte[] EncodeInitData()
        {
            var encoder = new ABIEncode();

            if (_modules.Count > 0)
            {
                var moduleConfigs = _modules.Select(m => new object[]
                {
                    m.moduleTypeId,
                    m.moduleAddress,
                    m.initData
                }).ToArray();

                return encoder.GetABIEncoded(
                    new ABIValue("tuple(uint256,address,bytes)[]", moduleConfigs));
            }

            if (!string.IsNullOrEmpty(_validatorAddress) && !string.IsNullOrEmpty(_ownerAddress))
            {
                var ownerInitData = encoder.GetABIEncoded(new ABIValue("address", _ownerAddress));
                return encoder.GetABIEncoded(
                    new ABIValue("tuple(uint256,address,bytes)[]", new object[][]
                    {
                        new object[] { 1, _validatorAddress, ownerInitData }
                    }));
            }

            if (!string.IsNullOrEmpty(_ownerAddress))
            {
                return encoder.GetABIEncoded(new ABIValue("address", _ownerAddress));
            }

            return Array.Empty<byte>();
        }
    }
}
