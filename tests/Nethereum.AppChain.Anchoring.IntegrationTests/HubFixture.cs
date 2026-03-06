using System.Numerics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Server.Accounts;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.DevChain.Server.Server;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    public class HubFixture : IAsyncLifetime
    {
        private WebApplication? _app;
        private Task? _runTask;

        public const string OwnerPrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public const string OwnerAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        public const string SequencerPrivateKey = "59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
        public const string SequencerAddress = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

        public const string SenderPrivateKey = "5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a";
        public const string SenderAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        public const int ChainId = 31337;
        public const int Port = 18546;
        public const ulong AppChainId = 420420;
        public string Url => $"http://127.0.0.1:{Port}";

        public Nethereum.Web3.Web3 OwnerWeb3 { get; private set; } = null!;
        public Nethereum.Web3.Web3 SequencerWeb3 { get; private set; } = null!;
        public Nethereum.Web3.Web3 SenderWeb3 { get; private set; } = null!;
        public string HubContractAddress { get; private set; } = null!;
        public AppChainHubService OwnerHubService { get; private set; } = null!;
        public AppChainHubService SequencerHubService { get; private set; } = null!;
        public AppChainHubService SenderHubService { get; private set; } = null!;

        public static readonly BigInteger RegistrationFee = BigInteger.Parse("10000000000000000"); // 0.01 ETH
        public static readonly BigInteger MessageFee = BigInteger.Parse("1000000000000000"); // 0.001 ETH
        public static readonly BigInteger HubFeeBps = 1000; // 10%

        public async Task InitializeAsync()
        {
            var config = new DevChainServerConfig
            {
                Port = Port,
                ChainId = ChainId
            };

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddDevChainServer(config);
            builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

            _app = builder.Build();

            var node = _app.Services.GetRequiredService<DevChainNode>();
            var accountManager = _app.Services.GetRequiredService<DevAccountManager>();
            var dispatcher = _app.Services.GetRequiredService<RpcDispatcher>();

            await node.StartAsync(accountManager.Accounts.Select(a => a.Address));

            _app.MapPost("/", async (HttpContext httpContext) =>
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var json = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(json))
                {
                    httpContext.Response.StatusCode = 400;
                    return;
                }

                httpContext.Response.ContentType = "application/json";

                if (json.TrimStart().StartsWith('['))
                {
                    var requests = JsonSerializer.Deserialize(json, CoreChainJsonContext.Default.JsonRpcRequestArray);
                    if (requests != null)
                    {
                        var rpcRequests = requests.Select(ToRpcRequestMessage).ToArray();
                        var responses = await dispatcher.DispatchBatchAsync(rpcRequests);
                        var jsonResponses = responses.Select(ToJsonRpcResponse).ToArray();
                        await httpContext.Response.WriteAsync(
                            JsonSerializer.Serialize(jsonResponses, CoreChainJsonContext.Default.JsonRpcResponseArray));
                        return;
                    }
                }

                var request = JsonSerializer.Deserialize(json, CoreChainJsonContext.Default.JsonRpcRequest);
                if (request == null) { httpContext.Response.StatusCode = 400; return; }

                var rpcRequest = ToRpcRequestMessage(request);
                var response = await dispatcher.DispatchAsync(rpcRequest);
                var jsonResponse = ToJsonRpcResponse(response);
                await httpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(jsonResponse, CoreChainJsonContext.Default.JsonRpcResponse));
            });

            _runTask = Task.Run(async () =>
            {
                try { await _app.RunAsync($"http://127.0.0.1:{Port}"); }
                catch (OperationCanceledException) { }
            });

            await WaitForServerReadyAsync();

            var ownerAccount = new Nethereum.Web3.Accounts.Account(OwnerPrivateKey, ChainId);
            ownerAccount.TransactionManager.UseLegacyAsDefault = true;
            OwnerWeb3 = new Nethereum.Web3.Web3(ownerAccount, Url);

            var seqAccount = new Nethereum.Web3.Accounts.Account(SequencerPrivateKey, ChainId);
            seqAccount.TransactionManager.UseLegacyAsDefault = true;
            SequencerWeb3 = new Nethereum.Web3.Web3(seqAccount, Url);

            var senderAccount = new Nethereum.Web3.Accounts.Account(SenderPrivateKey, ChainId);
            senderAccount.TransactionManager.UseLegacyAsDefault = true;
            SenderWeb3 = new Nethereum.Web3.Web3(senderAccount, Url);

            await DeployHubContractAsync();
        }

        private async Task DeployHubContractAsync()
        {
            var deployment = new AppChainHubDeployment
            {
                RegistrationFee = RegistrationFee,
                MessageFee = MessageFee,
                HubFeeBps = HubFeeBps
            };

            var receipt = await AppChainHubService.DeployContractAndWaitForReceiptAsync(OwnerWeb3, deployment);
            HubContractAddress = receipt.ContractAddress!;

            OwnerHubService = new AppChainHubService(OwnerWeb3, HubContractAddress);
            SequencerHubService = new AppChainHubService(SequencerWeb3, HubContractAddress);
            SenderHubService = new AppChainHubService(SenderWeb3, HubContractAddress);
        }

        public byte[] SignRegistration(ulong chainId, string ownerAddress)
        {
            var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(
                System.Text.Encoding.UTF8.GetBytes("").Length == 0
                    ? new byte[0]
                    : new byte[0]);

            var packed = new byte[8 + 20];
            var chainIdBytes = BitConverter.GetBytes(chainId);
            if (BitConverter.IsLittleEndian) Array.Reverse(chainIdBytes);
            Array.Copy(chainIdBytes, 0, packed, 0, 8);

            var addressBytes = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions
                .HexToByteArray(ownerAddress.Replace("0x", ""));
            Array.Copy(addressBytes, 0, packed, 8, 20);

            var messageHash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(packed);
            var signer = new EthereumMessageSigner();
            var signature = signer.Sign(messageHash, SequencerPrivateKey);
            return Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions
                .HexToByteArray(signature.Replace("0x", ""));
        }

        public async Task RegisterAppChainAsync()
        {
            var signature = SignRegistration(AppChainId, OwnerAddress);
            var registerFunction = new RegisterAppChainFunction
            {
                ChainId = AppChainId,
                Sequencer = SequencerAddress,
                SequencerSignature = signature,
                AmountToSend = RegistrationFee
            };
            await OwnerHubService.RegisterAppChainRequestAndWaitForReceiptAsync(registerFunction);
        }

        public async Task DisposeAsync()
        {
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }

            if (_runTask != null)
            {
                try { await _runTask; }
                catch (OperationCanceledException) { }
            }
        }

        private async Task WaitForServerReadyAsync(int maxRetries = 50)
        {
            using var client = new HttpClient();
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var content = new StringContent(
                        "{\"jsonrpc\":\"2.0\",\"method\":\"eth_chainId\",\"params\":[],\"id\":1}",
                        System.Text.Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(Url, content);
                    if (response.IsSuccessStatusCode) return;
                }
                catch { }
                await Task.Delay(100);
            }
            throw new Exception($"Server did not become ready at {Url}");
        }

        private static RpcRequestMessage ToRpcRequestMessage(JsonRpcRequest request)
        {
            return new RpcRequestMessage
            {
                Id = request.Id,
                Method = request.Method,
                JsonRpcVersion = request.Jsonrpc,
                RawParameters = request.Params.HasValue ? request.Params.Value : null
            };
        }

        private static JsonRpcResponse ToJsonRpcResponse(RpcResponseMessage response)
        {
            if (response.HasError)
            {
                return new JsonRpcResponse
                {
                    Id = response.Id,
                    Error = new JsonRpcError
                    {
                        Code = response.Error.Code,
                        Message = response.Error.Message,
                        Data = response.Error.Data
                    }
                };
            }
            return new JsonRpcResponse { Id = response.Id, Result = response.Result };
        }
    }
}
