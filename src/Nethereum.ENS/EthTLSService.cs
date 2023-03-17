using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.ENS.ETHRegistrarController.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ENS
{

    public class EthTLSService
    {
        public EthTLSService(Web3.Web3 web3, string ensRegistryAddress = "0x00000000000C2E074eC69A0dFb2997BA6C7d2e1e")
        {
            if (string.IsNullOrEmpty(ensRegistryAddress))
                throw new ArgumentException("ensRegistryAddress cannot be null", nameof(ensRegistryAddress));
            Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            EnsRegistryAddress = ensRegistryAddress;
            _ensUtil = new EnsUtil();
            TLS = "eth";
            TLSNameHash = _ensUtil.GetNameHash(TLS).HexToByteArray();
        }

        public Web3.Web3 Web3 { get; }
        public string EnsRegistryAddress { get; }
        public string TLS { get; private set; }
        public byte[] TLSNameHash { get; private set; }
        public string TLSRegisterAddress { get; private set; }
        public string TLSResolverAddress { get; private set; }
        public string TLSControllerAddress { get; private set; }
        public ENSRegistryService ENSRegistryService { get; private set; }
        public PublicResolverService TLSResolverService { get; private set; }
        public ETHRegistrarControllerService TLSRegistrarControllerService { get; private set; }
        public int MinimunDurationRegistrationInSeconds { get; private set; }

        private readonly EnsUtil _ensUtil;

        public async Task InitialiseAsync()
        {
            ENSRegistryService = new ENSRegistryService(Web3, EnsRegistryAddress);
            TLSRegisterAddress = await ENSRegistryService.OwnerQueryAsync(TLSNameHash).ConfigureAwait(false);
            TLSResolverAddress = await ENSRegistryService.ResolverQueryAsync(TLSNameHash).ConfigureAwait(false);
            TLSResolverService = new PublicResolverService(Web3, TLSResolverAddress);
            TLSControllerAddress = await TLSResolverService.InterfaceImplementerQueryAsync(TLSNameHash, "0x018fac06".HexToByteArray()).ConfigureAwait(false);
            TLSRegistrarControllerService = new ETHRegistrarControllerService(Web3, TLSControllerAddress);
            MinimunDurationRegistrationInSeconds = (int) await TLSRegistrarControllerService.MIN_REGISTRATION_DURATIONQueryAsync().ConfigureAwait(false);
        }

        public int GetMinimumDurationInDays()
        {
            return ConvertDurationToDays(MinimunDurationRegistrationInSeconds);
        }

        public async Task<BigInteger> CalculateRentPriceAsync(string name, int durationInDays)
        {
            var duration = ConvertDurationInSecondsValidatingMinimum(durationInDays);
            return await TLSRegistrarControllerService.RentPriceQueryAsync(name, duration).ConfigureAwait(false);
        }

        public async Task<decimal> CalculateRentPriceInEtherAsync(string name, int durationInDays)
        {
            var rentPriceWei = await CalculateRentPriceAsync(name, durationInDays).ConfigureAwait(false);
            return Util.UnitConversion.Convert.FromWei(rentPriceWei);
        }

        public int ConvertDurationToSeconds(int durationInDays)
        {
            return 60 * 60 * 24 * durationInDays;
        }

        public int ConvertDurationToDays(int durationInSeconds)
        {
            return durationInSeconds / (60 * 60 * 24);
        }

        public Task<bool> IsNameAvailableAsync(string name)
        {
            return TLSRegistrarControllerService.AvailableQueryAsync(name);
        }

        private int ConvertDurationInSecondsValidatingMinimum(int durationInDays)
        {
            var duration = ConvertDurationToSeconds(durationInDays);
            if (duration < MinimunDurationRegistrationInSeconds) throw new Exception($"Duration has to be bigger than the minimum duration: {GetMinimumDurationInDays()} days");
            return duration;
        }

        public byte[] ConvertSecretToHash(string secret)
        {
            return Util.Sha3Keccack.Current.CalculateHash(secret).HexToByteArray();
        }

        public Task<byte[]> CalculateCommitmentAsync(string name, string owner, string secret)
        {
            var fullSecret = ConvertSecretToHash(secret);
            return TLSRegistrarControllerService.MakeCommitmentQueryAsync(name, owner, fullSecret);
        }
    
        public async Task<string> CommitRequestAsync(string name, string owner, string secret)
        {
            var commitment = await CalculateCommitmentAsync(name, owner, secret).ConfigureAwait(false);
            return await TLSRegistrarControllerService.CommitRequestAsync(commitment).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> CommitRequestAndWaitForReceiptAsync(string name, string owner, string secret)
        {
            var commitment = await CalculateCommitmentAsync(name, owner, secret).ConfigureAwait(false);
            return await TLSRegistrarControllerService.CommitRequestAndWaitForReceiptAsync(commitment).ConfigureAwait(false);
        }

        public Task<string> RegisterRequestAsync(string name, string owner, int durationInDays, string secret, decimal etherRentPrice)
        {
            var weiPrice = Util.UnitConversion.Convert.ToWei(etherRentPrice);
            return RegisterRequestAsync(name, owner, durationInDays, secret, weiPrice);
        }


        public Task<string> RegisterRequestAsync(string name, string owner, int durationInDays, string secret, BigInteger weiRentPrice)
        {
            var registerFunction = CreateRegisterFunction(name, owner, durationInDays, secret, weiRentPrice);
            return TLSRegistrarControllerService.RegisterRequestAsync(registerFunction);
        }

        private RegisterFunction CreateRegisterFunction(string name, string owner, int durationInDays, string secret, BigInteger weiRentPrice)
        {
            var fullSecret = ConvertSecretToHash(secret);
            var duration = ConvertDurationInSecondsValidatingMinimum(durationInDays);
            var registerFunction = new RegisterFunction()
            {
                Name = name,
                AmountToSend = weiRentPrice,
                Duration = duration,
                Owner = owner,
                Secret = fullSecret
            };
            return registerFunction;
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(string name, string owner, int durationInDays, string secret, decimal etherRentPrice)
        {
            var weiPrice = Util.UnitConversion.Convert.ToWei(etherRentPrice);
            return RegisterRequestAndWaitForReceiptAsync(name, owner, durationInDays, secret, weiPrice);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(string name, string owner, int durationInDays, string secret, BigInteger weiRentPrice)
        {
            var registerFunction = CreateRegisterFunction(name, owner, durationInDays, secret, weiRentPrice);
            return TLSRegistrarControllerService.RegisterRequestAndWaitForReceiptAsync(registerFunction);
        }

        public Task<string> CommitRequestAsync(byte[] commitment)
        {
            return TLSRegistrarControllerService.CommitRequestAsync(commitment);
        }

        public Task<TransactionReceipt> CommitRequestAndWaitForReceiptAsync(byte[] commitment)
        {
            return TLSRegistrarControllerService.CommitRequestAndWaitForReceiptAsync(commitment);
        }

        public Task<string> RenewRequestAsync(string name, int durationInDays, decimal etherRentPrice)
        {
            var weiPrice = Util.UnitConversion.Convert.ToWei(etherRentPrice);
            return RenewRequestAsync(name, durationInDays, weiPrice);
        }

        public Task<string> RenewRequestAsync(string name, int durationInDays, BigInteger weiRentPrice)
        {
            var renewFunction = CreateRenewFunction(name, durationInDays, weiRentPrice);
            return TLSRegistrarControllerService.RenewRequestAsync(renewFunction);
        }

        private RenewFunction CreateRenewFunction(string name, int durationInDays, BigInteger weiPrice)
        {
            var duration = ConvertDurationInSecondsValidatingMinimum(durationInDays);
            var renewFunction = new RenewFunction()
            {
                AmountToSend = weiPrice,
                Duration = duration,
                Name = name
            };
            return renewFunction;
        }
    }
}
