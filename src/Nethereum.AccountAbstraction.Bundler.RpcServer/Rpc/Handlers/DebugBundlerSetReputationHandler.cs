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
                        OpsSeen = (int)ParseHexOrDecimalLong(entry.OpsSeen),
                        OpsIncluded = (int)ParseHexOrDecimalLong(entry.OpsIncluded),
                        OpsFailed = (int)ParseHexOrDecimalLong(entry.OpsFailed),
                        Status = ParseStatus(entry.Status)
                    };

                    await _bundler.SetReputationAsync(entry.Address, reputation);
                }

                return Success(request.Id, "ok");
            }
            catch (RpcException ex)
            {
                return Error(request.Id, ex.Code, ex.Message, ex.Data);
            }
            catch (JsonException ex)
            {
                return Error(request.Id, BundlerErrorCodes.InvalidFields, $"Invalid reputation format: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}");
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

            // ERC-4337 debug_bundler_setReputation sends these as hex-quantity strings
            // (e.g. "0x10"), not JSON numbers; deserialize loosely and parse via
            // ParseHexOrDecimalLong so both forms are accepted.
            public JsonElement OpsSeen { get; set; }
            public JsonElement OpsIncluded { get; set; }
            public JsonElement OpsFailed { get; set; }
            public string? Status { get; set; }
        }
    }
}
