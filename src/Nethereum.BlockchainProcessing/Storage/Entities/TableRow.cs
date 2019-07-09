using System;

namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public class TableRow
    {
        public int RowIndex { get; set; }

        public bool IsNew() => RowIndex == 0;

        public DateTime? RowCreated { get; set; }

        public DateTime? RowUpdated { get; set; }

        public void UpdateRowDates()
        {
            var now = DateTime.UtcNow;

            if (RowCreated == null)
            {
                RowCreated = now;
            }

            RowUpdated = now;
        }
    }
}