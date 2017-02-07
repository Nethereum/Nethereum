using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Geth;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.StandardTokenEIP20;
using Nethereum.StandardTokenEIP20.Events.DTO;
using Xunit;

namespace Nethereum.Web3.Tests.StandardToken
{
    public class Erc20TokenTester
    {
        [Fact]
        public async void Test()
        {
           
                var contractByteCode =
                    "0x60606040526040516020806106f5833981016040528080519060200190919050505b80600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005081905550806000600050819055505b506106868061006f6000396000f360606040523615610074576000357c010000000000000000000000000000000000000000000000000000000090048063095ea7b31461008157806318160ddd146100b657806323b872dd146100d957806370a0823114610117578063a9059cbb14610143578063dd62ed3e1461017857610074565b61007f5b610002565b565b005b6100a060048080359060200190919080359060200190919050506101ad565b6040518082815260200191505060405180910390f35b6100c36004805050610674565b6040518082815260200191505060405180910390f35b6101016004808035906020019091908035906020019091908035906020019091905050610281565b6040518082815260200191505060405180910390f35b61012d600480803590602001909190505061048d565b6040518082815260200191505060405180910390f35b61016260048080359060200190919080359060200190919050506104cb565b6040518082815260200191505060405180910390f35b610197600480803590602001909190803590602001909190505061060b565b6040518082815260200191505060405180910390f35b600081600260005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008573ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925846040518082815260200191505060405180910390a36001905061027b565b92915050565b600081600160005060008673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561031b575081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505410155b80156103275750600082115b1561047c5781600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff168473ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a381600160005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505403925050819055506001905061048656610485565b60009050610486565b5b9392505050565b6000600160005060008373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505490506104c6565b919050565b600081600160005060003373ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561050c5750600082115b156105fb5781600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a36001905061060556610604565b60009050610605565b5b92915050565b6000600260005060008473ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005054905061066e565b92915050565b60006000600050549050610683565b9056";
                var abi =
                    @"[{""constant"":false,""inputs"":[{""name"":""_spender"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":""success"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""totalSupply"",""outputs"":[{""name"":""supply"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""_from"",""type"":""address""},{""name"":""_to"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""name"":""success"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":""_owner"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":""balance"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""_to"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""name"":""success"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":""_owner"",""type"":""address""},{""name"":""_spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""name"":""remaining"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""_initialAmount"",""type"":""uint256""}],""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""_from"",""type"":""address""},{""indexed"":true,""name"":""_to"",""type"":""address""},{""indexed"":false,""name"":""_value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""_owner"",""type"":""address""},{""indexed"":true,""name"":""_spender"",""type"":""address""},{""indexed"":false,""name"":""_value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""}]";
                var addressOwner = "0x12890d2cce102216644c59dae5baed380d84830c";

                var web3 = new Web3Geth(ClientFactory.GetClient());
            try
            {
                var eth = web3.Eth;
                var transactions = eth.Transactions;
                ulong totalSupply = 1000000;

                var pass = "password";
                var result =
                    await web3.Personal.UnlockAccount.SendRequestAsync(addressOwner, pass, 600);
                Assert.True(result, "Account should be unlocked");
                var newAddress = await web3.Personal.NewAccount.SendRequestAsync(pass);

                Assert.NotNull(newAddress);
                Console.WriteLine(newAddress);

                var transactionHash =
                    await
                        eth.DeployContract.SendRequestAsync(abi, contractByteCode, addressOwner,
                            new HexBigInteger(900000), totalSupply);

                result = await web3.Miner.Start.SendRequestAsync();
                Assert.True(result, "Mining should have started");

                //get the contract address 
                var receipt = await GetTransactionReceiptAsync(transactions, transactionHash);

                var code = await web3.Eth.GetCode.SendRequestAsync(receipt.ContractAddress);

                if (String.IsNullOrEmpty(code))
                {
                    throw new Exception(
                        "Code was not deployed correctly, verify bytecode or enough gas was uto deploy the contract");
                }

                var tokenService = new StandardTokenService(web3, receipt.ContractAddress);

                var transfersEvent = tokenService.GetTransferEvent();
              

                var totalSupplyDeployed = await tokenService.GetTotalSupplyAsync<ulong>();
                Assert.Equal(totalSupply, totalSupplyDeployed);

                var ownerBalance = await tokenService.GetBalanceOfAsync<ulong>(addressOwner);
                Assert.Equal(totalSupply, ownerBalance);

               transactionHash = await tokenService.TransferAsync(addressOwner, newAddress, 1000);
              
               var transferReceipt = await GetTransactionReceiptAsync(transactions, transactionHash);

                ownerBalance = await tokenService.GetBalanceOfAsync<ulong>(addressOwner);
                Assert.Equal(totalSupply - 1000, ownerBalance);

                var newAddressBalance = await tokenService.GetBalanceOfAsync<ulong>(newAddress);
                Assert.Equal((ulong) 1000, newAddressBalance);

                var allTransfersFilter = await transfersEvent.CreateFilterAsync(new BlockParameter(transferReceipt.BlockNumber));
                var eventLogsAll = await transfersEvent.GetAllChanges<Transfer>(allTransfersFilter);
                Assert.Equal(1, eventLogsAll.Count);
                var transferLog = eventLogsAll.First();
                Assert.Equal(transferLog.Log.TransactionIndex.HexValue, transferReceipt.TransactionIndex.HexValue);
                Assert.Equal(transferLog.Log.BlockNumber.HexValue, transferReceipt.BlockNumber.HexValue);
                Assert.Equal(transferLog.Event.AddressTo, newAddress);
                Assert.Equal(transferLog.Event.Value, (ulong)1000);

            }
            finally
            {
                var result = await web3.Miner.Stop.SendRequestAsync();
                Assert.True(result, "Mining should have stop");
                result = await web3.Personal.LockAccount.SendRequestAsync(addressOwner);
                Assert.True(result, "Account should be locked");
            }
           
        }

        private static async Task<TransactionReceipt> GetTransactionReceiptAsync(EthApiTransactionsService transactionService, string transactionHash)
        {
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                await Task.Delay(1000);
                receipt = await transactionService.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }
            return receipt;
        }
    }
}
