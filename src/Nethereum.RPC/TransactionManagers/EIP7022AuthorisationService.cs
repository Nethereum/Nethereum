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
        protected ITransactionManager TransactionManager { get; }
        protected IEthApiService EthApiService { get; }

        public const string CODE_EIP7022_PREFIX = "0xef0100";

        public EIP7022AuthorisationService(ITransactionManager transactionManager, IEthApiService ethApiService)
        {
            TransactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
            EthApiService = ethApiService;
        }

        private TransactionInput CreateDefaultTransactionInputForExistingAddress()
        {
           
            return new TransactionInput()
            {
                From = TransactionManager.Account.Address,
                // The transaction manager will add up the authorisation delegation gas,
                // but we top it up with 2300 to allow for the possible receiver amount
                Gas = new HexBigInteger(TransactionManager.DefaultGas + 2300),
                To = TransactionManager.Account.Address,
                Value = new HexBigInteger(0),
                Data = "0x"
            };
        }

     

        public async Task Add7022AuthorisationDelegationOnNextRequestAsync(string addressContract, bool useUniversalZeroChainId = false)
        {
            await TransactionManager.Add7022AuthorisationDelegationOnNextRequestAsync(addressContract, useUniversalZeroChainId).ConfigureAwait(false);
        }

        public void Remove7022AuthorisationDelegationOnNextRequest()
        {
            TransactionManager.Remove7022AuthorisationDelegationOnNextRequest();
        }

        public async Task<string> AuthoriseRequestAsync(string addressContract, bool useUniversalZeroChainId = false)
        {
            await TransactionManager.Add7022AuthorisationDelegationOnNextRequestAsync(addressContract, useUniversalZeroChainId).ConfigureAwait(false);
            return await TransactionManager.SendTransactionAsync(CreateDefaultTransactionInputForExistingAddress()).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> AuthoriseRequestAndWaitForReceiptAsync(string addressContract, bool useUniversalZeroChainId = false)
        {
            await TransactionManager.Add7022AuthorisationDelegationOnNextRequestAsync(addressContract, useUniversalZeroChainId).ConfigureAwait(false);
            return await TransactionManager.SendTransactionAndWaitForReceiptAsync(CreateDefaultTransactionInputForExistingAddress()).ConfigureAwait(false);
        }

        public async Task<string> RemoveAuthorisationRequestAsync()
        {
            TransactionManager.Remove7022AuthorisationDelegationOnNextRequest();
            return await TransactionManager.SendTransactionAsync(CreateDefaultTransactionInputForExistingAddress()).ConfigureAwait(false);
        }

       


        public async Task<TransactionReceipt> RemoveAuthorisationRequestAndWaitForReceiptAsync()
        {
            TransactionManager.Remove7022AuthorisationDelegationOnNextRequest();
            return await TransactionManager.SendTransactionAndWaitForReceiptAsync(CreateDefaultTransactionInputForExistingAddress()).ConfigureAwait(false);
        }

        public async Task<string> GetDelegatedAccountAddressAsync(string address)
        {
            var code = await EthApiService.GetCode.SendRequestAsync(address).ConfigureAwait(false);
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
            var code = await EthApiService.GetCode.SendRequestAsync(address).ConfigureAwait(false);
            if (string.IsNullOrEmpty(code) || code == "0x")
            {
                return false;
            }
            code = code.EnsureHexPrefix().ToLower();
            return code.StartsWith(CODE_EIP7022_PREFIX);
        }

    }
}
