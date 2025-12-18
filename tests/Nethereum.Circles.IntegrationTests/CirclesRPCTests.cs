using Nethereum.Circles.Contracts.Hub;
using Nethereum.Circles.RPC.Requests;
using Nethereum.Circles.RPC.Requests.DTOs;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Circles.IntegrationTests
{
    public class CirclesRPCTests
    {

        
        string humanAddress1 = "0xed1067bc2a09dd6a146eccd3577f27eb5be93646";
        string humanAddress2 = "0x42cEDde51198D1773590311E2A340DC06B24cB37";

        [Fact]
        public async Task ShouldGetBalances()
        {
            
            var client = new RpcClient(new Uri("https://rpc.aboutcircles.com/"));

            // GetTotalBalance
            var getTotalBalance = new GetTotalBalance(client);
            string balance = await getTotalBalance.SendRequestAsync(humanAddress1);
            Debug.WriteLine($"Total Balance: {balance}");

            // GetTotalBalanceV2
            var getTotalBalanceV2 = new GetTotalBalanceV2(client);
            string balanceV2 = await getTotalBalanceV2.SendRequestAsync(humanAddress1);
            Debug.WriteLine($"Total Balance (V2): {balanceV2}");
        }

        [Fact]
        public async Task ShouldGetTransactionHistoryAsync()
        {
            //var avatar = "0xc5d6c75087780e0c18820883cf5a580bb3a4d834";
            var avatar = "0xed1067bc2a09dd6a146eccd3577f27eb5be93646";
            //var client = new RpcClient(new Uri("https://chiado-rpc.aboutcircles.com"));
            var client = new RpcClient(new Uri("https://rpc.aboutcircles.com/"));
            var circlesQuery = new CirclesQuery<TransactionHistoryRow>(client);

            

            var transactionHistoryQuery = new GetTransactionHistoryQuery(client);
            var transactions = await transactionHistoryQuery.SendRequestAsync(avatar, 100);

            foreach (var transaction in transactions.Response)
            {
                Debug.WriteLine($"Transaction Hash: {transaction.TransactionHash}, Block {transaction.BlockNumber}, Value: {transaction.Value}, {transaction.Operator}, {transaction.To}");
            }

            transactions = await transactionHistoryQuery.MoveNextPageAsync(transactions);

            foreach (var transaction in transactions.Response)
            {
                Debug.WriteLine($"Transaction Hash: {transaction.TransactionHash}, Value: {transaction.Value}");
            }

        }

        [Fact]
        public async Task ShouldGetTrustRelationsAsync()
        {
            //var avatar = "0xc5d6c75087780e0c18820883cf5a580bb3a4d834";
            var avatar = "0xed1067bc2a09dd6a146eccd3577f27eb5be93646";
            //var client = new RpcClient(new Uri("https://chiado-rpc.aboutcircles.com"));
            var client = new RpcClient(new Uri("https://rpc.aboutcircles.com/"));
            var circlesQuery = new CirclesQuery<TransactionHistoryRow>(client);



            var trustRelationsQuery = new GetTrustRelationsQuery(client);
            var trustRelations = await trustRelationsQuery.SendRequestAsync(avatar, 20);

            foreach (var trustRelation in trustRelations.Response)
            {
                Debug.WriteLine($"Trustee: {trustRelation.Trustee}, Truster: {trustRelation.Truster}, {trustRelation.Version}");
            }

            trustRelations = await trustRelationsQuery.MoveNextPageAsync(trustRelations);

            foreach (var trustRelation in trustRelations.Response)
            {
                Debug.WriteLine($"Trustee: {trustRelation.Trustee}, Truster: {trustRelation.Truster}, {trustRelation.Version}");
            }

        }


        [Fact]
        public async Task ShouldGetAvatarInfoAsync()
        {
            //var avatar = "0xc5d6c75087780e0c18820883cf5a580bb3a4d834";
            var avatar = "0xed1067bc2a09dd6a146eccd3577f27eb5be93646";
            //var client = new RpcClient(new Uri("https://chiado-rpc.aboutcircles.com"));
            var client = new RpcClient(new Uri("https://rpc.aboutcircles.com/"));
            var circlesQuery = new CirclesQuery<TransactionHistoryRow>(client);



            var query = new GetAvatarInfoQuery(client);
            var response = await query.SendRequestAsync(avatar, 20);

            foreach (var avatarInfo in response.Response)
            {
                Debug.WriteLine($" Name: {avatarInfo.Name}, Avatar: {avatarInfo.Avatar}, Token: {avatarInfo.TokenId}, Version:{avatarInfo.Version}");
            }

            response = await query.MoveNextPageAsync(response);

            foreach (var avatarInfo in response.Response)
            {
                Debug.WriteLine($" Name: {avatarInfo.Name}, Avatar: {avatarInfo.Avatar}, Token: {avatarInfo.TokenId}, Version:{avatarInfo.Version}");
            }

        }
    }
}
