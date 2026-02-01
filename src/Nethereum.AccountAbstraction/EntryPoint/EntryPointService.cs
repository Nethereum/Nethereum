using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.Signer;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.EntryPoint
{
        public partial class EntryPointService
        {
            public async Task<string> GetSenderAddressQueryAsync(byte[] initCode)
            {
                var getSenderAddressFunction = new GetSenderAddressFunction
                {
                    InitCode = initCode
                };
                return await ContractHandler.QueryAsync<GetSenderAddressFunction, string>(getSenderAddressFunction);
            }
       
            public async Task<string> HandleOpsQueryAsync(HandleOpsFunction handleOpsFunction)
            {
                return await ContractHandler.QueryAsync<HandleOpsFunction, string>(handleOpsFunction);
            }



            public async Task<UserOperation> InitialiseUserOperationAsync(
                UserOperation userOperation)
            {

                if (userOperation.InitCode != null && userOperation.InitCode.Length > 0)
                {
                    if (AccountAbstractionEIP7702Utils.IsEip7702UserOp(userOperation))
                    {
                        var delegateAddress = await Web3.Eth.GetEIP7022AuthorisationService().GetDelegatedAccountAddressAsync(userOperation.Sender);
                        if (string.IsNullOrEmpty(delegateAddress))
                        {
                            throw new Exception("Must provide eip7702delegate for EIP-7702 UserOperation initialisation");
                        }

                        if (userOperation.Nonce == null)
                        {
                            userOperation.Nonce = await Web3.Eth.Transactions
                                .GetTransactionCount
                                .SendRequestAsync(userOperation.Sender);
                        }
                    }
                    else
                    {
                            var initAddress = userOperation.InitCode.Take(20).ToArray();
                            var initCallData = userOperation.InitCode.Skip(20).ToArray();

                            if (userOperation.Nonce == null) userOperation.Nonce = 0;
                            if (string.IsNullOrEmpty(userOperation.Sender))
                            {
                                try
                                {
                                    userOperation.Sender = await GetSenderAddressQueryAsync(userOperation.InitCode);
                                }
                                catch (SmartContractCustomErrorRevertException ex)
                                {
                                    var error = FindCustomErrorException(ex);
                                    if (error is SmartContractCustomErrorRevertException<SenderAddressResultError> addressError)
                                    {
                                        userOperation.Sender = addressError.CustomError.Sender;
                                    }
                                    else
                                    {
                                        throw new Exception("Sender address not found in initCode");
                                    }
                                }
                            }

                            if (userOperation.VerificationGasLimit == null)
                            {
                                var senderCreator = await SenderCreatorQueryAsync();
                                string toAddress = null;
                                if (initAddress != null && initAddress.Length > 0)
                                {
                                    toAddress = initAddress.ToHex(true);
                                }

                                var initEstimate = await Web3.Eth.Transactions.EstimateGas.SendRequestAsync(
                                new CallInput
                                {
                                    From = senderCreator,
                                    To = toAddress,
                                    Data = initCallData.ToHex(true),
                                    Gas = new HexBigInteger(10000000)
                                });
                                userOperation.VerificationGasLimit = UserOperation.DEFAULT_VERIFICATION_GAS_LIMIT + initEstimate.Value;
                            }
                    }
                }

                if (userOperation.Nonce == null)
                {
                    if (string.IsNullOrEmpty(userOperation.Sender))
                        throw new Exception("Sender must be specified to fetch nonce");
                    userOperation.Nonce = await GetNonceQueryAsync(userOperation.Sender, 0);
                }

                if (userOperation.CallGasLimit == null && (userOperation.CallData != null && userOperation.CallData.Length > 0))
                {
                    var gasEstimated = await Web3.Eth.Transactions.EstimateGas.SendRequestAsync(
                        new CallInput
                        {
                            From = ContractAddress,
                            To = userOperation.Sender,
                            Data = userOperation.CallData.ToHex(true)
                        });
                    userOperation.CallGasLimit = gasEstimated.Value;
                }

                if (!string.IsNullOrEmpty(userOperation.Paymaster) && !userOperation.Paymaster.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
                {
                    userOperation.PaymasterVerificationGasLimit ??= UserOperation.DEFAULT_PAYMASTER_VERIFICATION_GAS_LIMIT;
                    userOperation.PaymasterPostOpGasLimit ??= UserOperation.DEFAULT_PAYMASTER_POST_OP_GAS_LIMIT;
                }

                if (userOperation.MaxPriorityFeePerGas == null)
                {
                    userOperation.MaxPriorityFeePerGas = UserOperation.DEFAULT_MAX_PRIORITY_FEE_PER_GAS;
                }

                if (userOperation.MaxFeePerGas == null)
                {
                    var block = await Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
                    userOperation.MaxFeePerGas = block.BaseFeePerGas.Value + userOperation.MaxPriorityFeePerGas.Value;
                }


                if (userOperation.PreVerificationGas == null)
                {
                    userOperation.SetNullValuesToDefaultValues();
                    userOperation.PreVerificationGas = UserOperation.DEFAULT_PRE_VERIFICATION_GAS;
                    var packedFilledOp = UserOperationBuilder.PackUserOperation(userOperation);
                    //what about the signature size?
                    userOperation.PreVerificationGas = userOperation.PreVerificationGas + CalculateCallDataCost(new ABIEncode().GetABIParamsEncoded(packedFilledOp));
                }

                userOperation.SetNullValuesToDefaultValues();

               

                return userOperation;
            }

            public async Task<PackedUserOperation> SignAndInitialiseUserOperationAsync(
                UserOperation userOperation,
                EthECKey signer,
                string eip7702Delegate = null)
            {
                var chainId = await Web3.Eth.ChainId.SendRequestAsync();
                userOperation = await InitialiseUserOperationAsync(userOperation);
                
                if (AccountAbstractionEIP7702Utils.IsEip7702UserOp(userOperation))
                {
                    if (string.IsNullOrEmpty(eip7702Delegate))
                    {
                        var delegateAddress = await Web3.Eth.GetEIP7022AuthorisationService().GetDelegatedAccountAddressAsync(userOperation.Sender);
                        userOperation.InitCode = AccountAbstractionEIP7702Utils.UpdateInitCodeForHashing(userOperation.InitCode, delegateAddress);
                    }
                    else
                    {
                        userOperation.InitCode = AccountAbstractionEIP7702Utils.UpdateInitCodeForHashing(userOperation.InitCode, eip7702Delegate.HexToByteArray());
                    }
                }
                
                
                  var packedUserOperation = UserOperationBuilder.PackAndSignEIP712UserOperation(userOperation, ContractAddress, chainId, signer);
                  return packedUserOperation;
            }

            private static BigInteger CalculateCallDataCost(byte[] data)
            {
                BigInteger cost = 0;
                foreach (var b in data)
                {
                    cost += b == 0 ? 4 : 16;
                }
                return cost;
            }
        }
    }


