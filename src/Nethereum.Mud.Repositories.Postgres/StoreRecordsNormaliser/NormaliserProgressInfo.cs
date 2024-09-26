using System.Numerics;

namespace Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser
{
    public class NormaliserProgressInfo
    {
        public long RowId { get; set; }
        public BigInteger BlockNumber { get; set; }
    }

}
