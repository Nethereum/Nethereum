using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    public interface IStateWriteBuffer
    {
        void BeginBuffering();

        Task FlushBufferAsync();

        Task DiscardBufferAsync();

        Task<FlatStateBatch> CaptureBufferAsync();
    }
}
