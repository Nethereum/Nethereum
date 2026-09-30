using System.Threading.Tasks;

namespace Nethereum.CoreChain.Engine
{
    public interface IPayloadBuildRegistry
    {
        string Register(Task<EnginePayloadBuild> build);

        Task<EnginePayloadBuild> Get(string payloadId);
    }
}
