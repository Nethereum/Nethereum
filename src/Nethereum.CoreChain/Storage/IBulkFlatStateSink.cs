using System;

namespace Nethereum.CoreChain.Storage
{
    public interface IBulkFlatStateSink : ISnapFlatStateWriter, IDisposable
    {
        void Flush();

        void Abandon();

        long RowsIngested { get; }

        bool HasBufferedRows { get; }
    }

    public interface IBulkFlatStateSinkProvider
    {
        IBulkFlatStateSink CreateBulkFlatSink();
    }
}
