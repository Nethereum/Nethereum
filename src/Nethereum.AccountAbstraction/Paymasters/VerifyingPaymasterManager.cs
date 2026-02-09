using System.Numerics;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.ABI.Encoders;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Paymasters
{
    public class VerifyingPaymasterManager : IVerifyingPaymasterManager
    {
        private readonly VerifyingPaymasterService _contractService;
        private readonly IWeb3 _web3;
        private readonly EthECKey? _signerKey;
        private string? _entryPointAddress;

        public string Address => _contractService.ContractAddress;
        public string EntryPointAddress => _entryPointAddress ?? throw new InvalidOperationException("EntryPoint not loaded. Call LoadAsync first.");

        public VerifyingPaymasterManager(IWeb3 web3, string paymasterAddress, EthECKey? signerKey = null)
        {
            _web3 = web3;
            _contractService = new VerifyingPaymasterService(web3, paymasterAddress);
            _signerKey = signerKey;
        }

        public static async Task<VerifyingPaymasterManager> LoadAsync(IWeb3 web3, string paymasterAddress, EthECKey? signerKey = null)
        {
            var service = new VerifyingPaymasterManager(web3, paymasterAddress, signerKey);
            await service.LoadEntryPointAsync();
            return service;
        }

        private async Task LoadEntryPointAsync()
        {
            _entryPointAddress = await _contractService.EntryPointQueryAsync();
        }

        public async Task<SponsorResult> SponsorUserOperationAsync(PackedUserOperation userOp, SponsorContext? context = null)
        {
            if (_signerKey == null)
            {
                return SponsorResult.Failure("No signer key configured");
            }

            var validUntil = context?.ValidUntil ?? (ulong)DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            var validAfter = context?.ValidAfter ?? 0;

            return await SponsorWithSignatureAsync(userOp, validUntil, validAfter, _signerKey);
        }

        public async Task<SponsorResult> SponsorWithSignatureAsync(
            PackedUserOperation userOp,
            ulong validUntil,
            ulong validAfter,
            EthECKey signerKey)
        {
            try
            {
                var hash = await GetHashAsync(userOp, validUntil, validAfter);
                var signature = signerKey.SignAndCalculateV(hash);
                var signatureBytes = EthECDSASignature.CreateStringSignature(signature).HexToByteArray();

                var paymasterAndData = BuildPaymasterAndData(validUntil, validAfter, signatureBytes);

                return SponsorResult.Success(paymasterAndData, Address, validUntil, validAfter);
            }
            catch (Exception ex)
            {
                return SponsorResult.Failure(ex.Message);
            }
        }

        public async Task<byte[]> GetHashAsync(PackedUserOperation userOp, ulong validUntil, ulong validAfter)
        {
            return await _contractService.GetHashQueryAsync(userOp, validUntil, validAfter);
        }

        public async Task<BigInteger> GetDepositAsync()
        {
            return await _contractService.GetDepositQueryAsync();
        }

        public async Task<TransactionReceipt> DepositAsync(BigInteger amount)
        {
            var function = new DepositFunction();
            function.AmountToSend = amount;
            return await _contractService.ContractHandler.SendRequestAndWaitForReceiptAsync(function);
        }

        public async Task<TransactionReceipt> WithdrawToAsync(string to, BigInteger amount)
        {
            return await _contractService.WithdrawToRequestAndWaitForReceiptAsync(to, amount);
        }

        private byte[] BuildPaymasterAndData(ulong validUntil, ulong validAfter, byte[] signature)
        {
            var addressBytes = Address.HexToByteArray();
            var validUntilBytes = new IntTypeEncoder().EncodePacked(validUntil);
            var validAfterBytes = new IntTypeEncoder().EncodePacked(validAfter);

            var result = new byte[addressBytes.Length + 6 + 6 + signature.Length];
            var offset = 0;

            Buffer.BlockCopy(addressBytes, 0, result, offset, addressBytes.Length);
            offset += addressBytes.Length;

            Buffer.BlockCopy(validUntilBytes, validUntilBytes.Length - 6, result, offset, 6);
            offset += 6;

            Buffer.BlockCopy(validAfterBytes, validAfterBytes.Length - 6, result, offset, 6);
            offset += 6;

            Buffer.BlockCopy(signature, 0, result, offset, signature.Length);

            return result;
        }
    }
}
