using System.Text.Json;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerSetReputationHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerSetReputationHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_setReputation";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var reputationJson = GetJsonElement(request, 0);
                var _entryPoint = GetOptionalParam<string>(request, 1, string.Empty);

                var entries = JsonSerializer.Deserialize<ReputationInputEntry[]>(
                    reputationJson.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? Array.Empty<ReputationInputEntry>();

                foreach (var entry in entries)
                {
                    var reputation = new ReputationEntry
                    {
                        Address = entry.Address,
                        OpsIncluded = entry.OpsIncluded,
                        OpsFailed = entry.OpsFailed,
                        Status = ParseStatus(entry.Status)
                    };

                    await _bundler.SetReputationAsync(entry.Address, reputation);
                }

                return Success(request.Id, "ok");
            }
            catch (JsonException ex)
            {
                return Error(request.Id, -32602, $"Invalid reputation format: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }

        private static ReputationStatus ParseStatus(string? status)
        {
            return status?.ToLowerInvariant() switch
            {
                "ok" => ReputationStatus.Ok,
                "throttled" => ReputationStatus.Throttled,
                "banned" => ReputationStatus.Banned,
                _ => ReputationStatus.Ok
            };
        }

        private class ReputationInputEntry
        {
            public string Address { get; set; } = null!;
            public int OpsIncluded { get; set; }
            public int OpsFailed { get; set; }
            public string? Status { get; set; }
        }
    }
}
