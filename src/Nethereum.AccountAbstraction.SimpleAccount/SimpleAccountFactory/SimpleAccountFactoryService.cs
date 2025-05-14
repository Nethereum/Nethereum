using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Signer;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory
{
    public partial class SimpleAccountFactoryService
    {
        public const ulong DEFAULT_ACCOUNT_CREATION_SALT = 0;
        public const ulong DEFAULT_ACCOUNT_CREATION_CALL_GAS_LIMIT = 1000000;
        public const ulong DEFAULT_ACCOUNT_CREATION_VERIFICATION_GAS_LIMIT = 2000000;
        public const ulong DEFAULT_ACCOUNT_CREATION_GAS = 10000000;

        public byte[] GetCreateAccountInitCode(BigInteger salt)
        {
            return GetCreateAccountInitCode(this.Web3.TransactionManager.Account.Address, salt);
        }

        public byte[] GetCreateAccountInitCode(string owner, BigInteger salt)
        {
            var createAccountFunction = new CreateAccountFunction();
            createAccountFunction.Owner = owner;
            createAccountFunction.Salt = salt;
            return this.ContractAddress.HexToByteArray().Concat(createAccountFunction.GetCallData()).ToArray();
        }

        public async Task<string> CreateAccountQueryAsync(string owner, BigInteger salt)
        {
            var createAccountFunction = new CreateAccountFunction();
            createAccountFunction.Owner = owner;
            createAccountFunction.Salt = salt;
            return await ContractHandler.QueryAsync<CreateAccountFunction, string>(createAccountFunction);
        }


        public class CreateAndDeployAccountResult
        {
            public string AccountAddress { get; set; }
            public TransactionReceipt Receipt { get; set; }
        }

        public Task<CreateAndDeployAccountResult> CreateAndDeployAccountAsync(
           string owner,
           string beneficiary,
           string entryPointAddress,
           EthECKey ethKey,
           decimal fundingAmountInEther = 0.01m,
           ulong salt = DEFAULT_ACCOUNT_CREATION_SALT,
           ulong callGasLimit = DEFAULT_ACCOUNT_CREATION_CALL_GAS_LIMIT,
           ulong verificationGasLimit = DEFAULT_ACCOUNT_CREATION_VERIFICATION_GAS_LIMIT,
           ulong gas = DEFAULT_ACCOUNT_CREATION_GAS
         )
        {
            return CreateAndDeployAccountAsync(
                owner,
                beneficiary,
                entryPointAddress,
                ethKey,
                fundingAmountInEther,
                new BigInteger(salt),
                new BigInteger(callGasLimit),
                new BigInteger(verificationGasLimit),
                new BigInteger(gas)
            );
        }



        public async Task<CreateAndDeployAccountResult> CreateAndDeployAccountAsync(
            string owner,
            string beneficiary,
            string entryPointAddress,
            EthECKey ethKey,
            decimal fundingAmountInEther,
            BigInteger salt,
            BigInteger callGasLimit,
            BigInteger verificationGasLimit,
            BigInteger gas
            )
        {
     
            var entryPointService = new EntryPointService(Web3, entryPointAddress);

            var accountAddress = await GetAddressQueryAsync(owner, salt);
            var initCode = GetCreateAccountInitCode(owner, salt);

            await Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(accountAddress, fundingAmountInEther);

            var code = await Web3.Eth.GetCode.SendRequestAsync(accountAddress);
            if (!string.IsNullOrEmpty(code) && code.RemoveHexPrefix().Length > 0)
                throw new Exception("Account already exists");

            var createOp = await entryPointService.SignAndInitialiseUserOperationAsync(new UserOperation()
            {
                InitCode = initCode,
                CallGasLimit = callGasLimit,
                VerificationGasLimit = verificationGasLimit
            }, ethKey);

            var handleOpsRequest = new HandleOpsFunction()
            {
                Ops = new List<PackedUserOperation>() { createOp },
                Beneficiary = owner,
                Gas = gas
            };

            var receipt = await entryPointService.HandleOpsRequestAndWaitForReceiptAsync(handleOpsRequest);

            var hash = await entryPointService.GetUserOpHashQueryAsync(createOp);
            var accountDeployed = receipt.Logs.DecodeAllEvents<AccountDeployedEventDTO>().FirstOrDefault();

            if (receipt.Status.Value != 1 || accountDeployed == null ||
                !hash.ToHex().IsTheSameHex(accountDeployed.Event.UserOpHash.ToHex()) ||
                !accountDeployed.Event.Sender.IsTheSameAddress(createOp.Sender) ||
                !accountDeployed.Event.Factory.IsTheSameAddress(initCode.ToHex().Substring(0, 40)))
            {
                throw new Exception("Account deployment validation failed");
            }

            return new CreateAndDeployAccountResult()
            {
                AccountAddress = accountAddress,
                Receipt = receipt
            };
        }
    }
}
