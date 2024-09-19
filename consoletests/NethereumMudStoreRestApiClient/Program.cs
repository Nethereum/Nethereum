using System.Net.Http;
using System.Net.Http.Headers;
using Nethereum.Mud.TableRepository;
using Nethereum.Mud;
using Nethereum.Util;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Web3;
using Nethereum.Util.Rest;
using static NethereumMudStoreRestApiClient.ItemTableRecord;
using Nethereum.RPC.Eth.DTOs;

namespace NethereumMudStoreRestApiClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var storedRecordRestApiClient = new StoredRecordRestApiClient(new RestHttpHelper(new HttpClient()), "https://localhost:7034");
            
            var address = "0xa3372F8dd68F9d9309bf9Ac95a88A27b3998ed4e";

            //default web3 as we are not using it
            var itemTableService = new ItemTableService(new Web3(), address);
            
            var predicate = itemTableService.CreateTablePredicateBuilder();

            var predicateResult = predicate.Equals(x => x.Id, 1).OrEqual(x => x.Id, 2).OrEqual(x => x.Id, 3).Expand();

            var itemTableRecords = await itemTableService.GetRecordsFromRepositoryAsync(predicateResult, storedRecordRestApiClient);
            foreach (var record in itemTableRecords)
            {
                Console.WriteLine("Id: " + record.Keys.Id);
                Console.WriteLine("Price: " + record.Values.Price);
                Console.WriteLine("Name: " + record.Values.Name);
                Console.WriteLine("Description: " + record.Values.Description);
            }
        }
    }


    public partial class ItemTableService : TableService<ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>
    {

        public ItemTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }

    public class ItemTableRecord : TableRecord<ItemKey, ItemValue>
    {
        public ItemTableRecord() : base("Item")
        {

        }

        public class ItemKey
        {
            [Parameter("uint32", "id", 1)]
            public int Id { get; set; }
        }

        public class ItemValue
        {
            [Parameter("uint32", "price", 1)]
            public int Price { get; set; }
            [Parameter("string", "name", 2)]
            public string Name { get; set; }
            [Parameter("string", "description", 3)]
            public string Description { get; set; }
            [Parameter("string", "owner", 4)]
            public string Owner { get; set; }
        }
    }



}
