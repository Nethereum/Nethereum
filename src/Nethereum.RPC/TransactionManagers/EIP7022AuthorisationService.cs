using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.TransactionManagers
{
    public class EIP7022AuthorisationService : IEIP7022AuthorisationService
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IEthApiService _ethApiService;

        public const string CODE_EIP7022_PREFIX = "0xef0100";

        public EIP7022AuthorisationService(ITransactionManager transactionManager, IEthApiService ethApiService)
        {
            _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
            _ethApiService = ethApiService;
        }

        private TransactionInput CreateDefaultTransactionInput()
        {
            //This is assuming that the contract it will be delegated to, will have a fallback function that will accept the transaction hence the gas
            return new TransactionInput()
            {
                From = _transactionManager.Account.Address,
                Gas =
                new HexBigInteger(_transactionManager.DefaultGas + 2300),
                To = _transactionManager.Account.Address,
                Value = new HexBigInteger(0),
                Data = "0x"
            };
        }

        public async Task Add7022AuthorisationDelegationOnNextRequestAsync(string addressContract, bool useUniversalZeroChainId = false)
        {
            await _transactionManager.Add7022AuthorisationDelegationOnNextRequestAsync(addressContract, useUniversalZeroChainId).ConfigureAwait(false);
        }

        public void Remove7022AuthorisationDelegationOnNextRequest()
        {
            _transactionManager.Remove7022AuthorisationDelegationOnNextRequest();
        }

        public async Task<string> AuthoriseRequestAsync(string addressContract, bool useUniversalZeroChainId = false)
        {
            await _transactionManager.Add7022AuthorisationDelegationOnNextRequestAsync(addressContract, useUniversalZeroChainId).ConfigureAwait(false);
            return await _transactionManager.SendTransactionAsync(CreateDefaultTransactionInput()).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> AuthoriseRequestAndWaitForReceiptAsync(string addressContract, bool useUniversalZeroChainId = false)
        {
            await _transactionManager.Add7022AuthorisationDelegationOnNextRequestAsync(addressContract, useUniversalZeroChainId).ConfigureAwait(false);
            return await _transactionManager.SendTransactionAndWaitForReceiptAsync(CreateDefaultTransactionInput()).ConfigureAwait(false);
        }

        public async Task<string> RemoveAuthorisationRequestAsync()
        {
            _transactionManager.Remove7022AuthorisationDelegationOnNextRequest();
            return await _transactionManager.SendTransactionAsync(CreateDefaultTransactionInput()).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> RemoveAuthorisationRequestAndWaitForReceiptAsync()
        {
            _transactionManager.Remove7022AuthorisationDelegationOnNextRequest();
            return await _transactionManager.SendTransactionAndWaitForReceiptAsync(CreateDefaultTransactionInput()).ConfigureAwait(false);
        }

        public async Task<string> GetDelegatedAccountAddressAsync(string address)
        {
            var code = await _ethApiService.GetCode.SendRequestAsync(address).ConfigureAwait(false);
            if (string.IsNullOrEmpty(code) || code == "0x")
            {
                return null;
            }

            code = code.EnsureHexPrefix().ToLower();
            if (!code.EnsureHexPrefix().ToLower().StartsWith(CODE_EIP7022_PREFIX))
            {
                throw new Exception($"The address {address} is not an EOA");
            }

            var delegateAddress = code.Substring(CODE_EIP7022_PREFIX.Length);
            return delegateAddress.ConvertToEthereumChecksumAddress();
        }

        public async Task<bool> IsDelegatedAccountAsync(string address)
        {
            var code = await _ethApiService.GetCode.SendRequestAsync(address).ConfigureAwait(false);
            if (string.IsNullOrEmpty(code) || code == "0x")
            {
                return false;
            }
            code = code.EnsureHexPrefix().ToLower();
            return code.StartsWith(CODE_EIP7022_PREFIX);
        }

    }
}
