using System.Collections.Generic;

namespace Nethereum.Mud.TableRepository
{
    public class TableRecordChangeSet<TTableRecord>
    where TTableRecord : ITableRecordSingleton, new()
    {
        public List<TTableRecord> Upserted { get; set; }
        public List<TTableRecord> Deleted { get; set; }
    }

}
