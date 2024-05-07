using Nethereum.Mud.Contracts.World;
using static Nethereum.Mud.Contracts.Tables.World.InstalledModulesTableRecord;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class InstalledModulesWorldServiceExtensions
    {
        public static async Task<InstalledModulesTableRecord> GetInstalledModulesTableRecord(this WorldService worldService, InstalledModulesKey key, BlockParameter blockParameter = null)
        {
            var table = new InstalledModulesTableRecord();
            table.Keys = key;
            return await worldService.GetRecordTableQueryAsync<InstalledModulesTableRecord, InstalledModulesKey, InstalledModulesValue>(table, blockParameter);
        }

        public static async Task<string> SetInstalledModulesTableRecordRequestAsync(this WorldService worldService, InstalledModulesKey key, bool isInstalled)
        {
            var table = new InstalledModulesTableRecord();
            table.Keys = key;
            table.Values = new InstalledModulesValue() { IsInstalled = isInstalled };
            return await worldService.SetRecordRequestAsync<InstalledModulesKey, InstalledModulesValue>(table);
        }

        public static async Task<TransactionReceipt> SetInstalledModulesTableRecordRequestAndWaitForReceiptAsync(this WorldService worldService, InstalledModulesKey key, bool isInstalled, CancellationTokenSource cancellationTokenSource = null)
        {
            var table = new InstalledModulesTableRecord();
            table.Keys = key;
            table.Values = new InstalledModulesValue() { IsInstalled = isInstalled };
            return await worldService.SetRecordRequestAndWaitForReceiptAsync<InstalledModulesKey, InstalledModulesValue>(table, cancellationTokenSource);
        }
    }
}