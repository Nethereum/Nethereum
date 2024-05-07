using Nethereum.Mud.Contracts.World;
using static Nethereum.Mud.Contracts.Tables.World.NamespaceOwnerTableRecord;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class NamespaceOwnerWorldServiceExtensions
    {
        public static async Task<NamespaceOwnerTableRecord> GetNamespaceOwnerTableRecord(this WorldService worldService, byte[] namespaceId, BlockParameter blockParameter = null)
        {
            var table = new NamespaceOwnerTableRecord();
            table.Keys = new NamespaceOwnerKey() { NamespaceId = namespaceId };
            return await worldService.GetRecordTableQueryAsync<NamespaceOwnerTableRecord, NamespaceOwnerKey, NamespaceOwnerValue>(table, blockParameter);
        }

        public static async Task<NamespaceOwnerTableRecord> GetNamespaceOwnerTableRecord(this WorldService worldService, NamespaceOwnerKey key, BlockParameter blockParameter = null)
        {
            var table = new NamespaceOwnerTableRecord();
            table.Keys = key;
            return await worldService.GetRecordTableQueryAsync<NamespaceOwnerTableRecord, NamespaceOwnerKey, NamespaceOwnerValue>(table, blockParameter);
        }

        //set record
        public static async Task<string> SetNamespaceOwnerTableRecordRequestAsync(this WorldService worldService, NamespaceOwnerTableRecord table)
        {
            return await worldService.SetRecordRequestAsync<NamespaceOwnerKey, NamespaceOwnerValue>(table);
        }

        //set record
        public static async Task<string> SetNamespaceOwnerTableRecordRequestAsync(this WorldService worldService, byte[] namespaceId, string owner)
        {
            var table = new NamespaceOwnerTableRecord();
            table.Keys = new NamespaceOwnerKey() { NamespaceId = namespaceId };
            table.Values = new NamespaceOwnerValue() { Owner = owner };
            return await worldService.SetRecordRequestAsync<NamespaceOwnerKey, NamespaceOwnerValue>(table);
        }

        public static async Task<string> SetNamespaceOwnerTableRecordRequestAsync(this WorldService worldService, NamespaceOwnerKey key, string owner)
        {
            var table = new NamespaceOwnerTableRecord();
            table.Keys = key;
            table.Values = new NamespaceOwnerValue() { Owner = owner };
            return await worldService.SetRecordRequestAsync<NamespaceOwnerKey, NamespaceOwnerValue>(table);
        }

        //set record and wait for receipt
        public static async Task<TransactionReceipt> SetNamespaceOwnerTableRecordRequestAndWaitForReceiptAsync(this WorldService worldService, NamespaceOwnerTableRecord table)
        {
            return await worldService.SetRecordRequestAndWaitForReceiptAsync<NamespaceOwnerKey, NamespaceOwnerValue>(table);
        }

        public static async Task<TransactionReceipt> SetNamespaceOwnerTableRecordRequestAndWaitForReceiptAsync(this WorldService worldService, byte[] namespaceId, string owner, CancellationTokenSource cancellationTokenSource = null)
        {
            var table = new NamespaceOwnerTableRecord();
            table.Keys = new NamespaceOwnerKey() { NamespaceId = namespaceId };
            table.Values = new NamespaceOwnerValue() { Owner = owner };
            return await worldService.SetRecordRequestAndWaitForReceiptAsync<NamespaceOwnerKey, NamespaceOwnerValue>(table, cancellationTokenSource);
        }

    }



    }
