using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nethereum.Circles.Contracts.Hub;
using Nethereum.Circles.Contracts.Hub.ContractDefinition;
using Nethereum.Circles.RPC.Requests;
using Nethereum.GnosisSafe;
using Nethereum.GnosisSafe.ContractDefinition;
using Nethereum.Model;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.Circles.IntegrationTests
{
    public class HubTests
    {
        /*
         v1HubAddress: "0x29b9a7fBb8995b2423a71cC17cf9810798F6C543",
         v2HubAddress: "0xFFfbD3E62203B888bb8E09c1fcAcE58242674964",
        */

        string v2HubAddress = "0xc12C1E50ABB450d6205Ea2C3Fa861b3B834d13e8";
        string humanAddress1 = "0xed1067bc2a09dd6a146eccd3577f27eb5be93646";
        int chainId = 100;

        [Fact]
        public async Task ShouldCalculateIssuance()
        {
            var web3 = new Nethereum.Web3.Web3("https://rpc.aboutcircles.com/");
            var hubService = new HubService(web3, v2HubAddress);
            var issuance = await hubService.CalculateIssuanceQueryAsync("0xed1067bc2a09dd6a146eccd3577f27eb5be93646");

        }

        [Fact]
        public async Task ShouldPersonalMint()
        {
            var privateKey = "";
            var web3 = new Nethereum.Web3.Web3(new Nethereum.Web3.Accounts.Account(privateKey), "https://rpc.aboutcircles.com/");
            var hubService = new HubService(web3, v2HubAddress);
            hubService.ChangeContractHandlerToSafeExecTransaction(humanAddress1, privateKey);

            var getTotalBalanceV2 = new GetTotalBalanceV2(web3.Client);
            string balanceV2 = await getTotalBalanceV2.SendRequestAsync(humanAddress1);
            Console.WriteLine($"Total Balance Before (V2): {balanceV2}");
            await hubService.PersonalMintRequestAndWaitForReceiptAsync();

            balanceV2 = await getTotalBalanceV2.SendRequestAsync(humanAddress1);
            Console.WriteLine($"Total Balance After (V2): {balanceV2}");
        }
    }



}
