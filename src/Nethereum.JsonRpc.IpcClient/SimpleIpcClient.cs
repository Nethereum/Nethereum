using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using Common.Logging;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.IpcClient
{
    /// <summary>
    /// IpcClient version that create a new NamedPipeClientStream per request
    /// and does not keep the connection open all the time.
    /// </summary>
    public class SimpleIpcClient : ClientBase
    {
        private readonly ILog _log;
        protected readonly string IpcPath;
        public JsonSerializerSettings JsonSerializerSettings { get; set; }

        public SimpleIpcClient(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null, ILog log = null)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();

            IpcPath = ipcPath;
            JsonSerializerSettings = jsonSerializerSettings;
            _log = log;
        }

        public async Task<int> ReceiveBufferedResponseAsync(NamedPipeClientStream client, byte[] buffer, CancellationToken cancellationToken)
        {
            return await client.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        public async Task<MemoryStream> ReceiveFullResponseAsync(NamedPipeClientStream client, CancellationToken cancellationToken)
        {
            MemoryStream memoryStream = new MemoryStream();
            byte[] buffer = new byte[512];
            for (int count = await ReceiveBufferedResponseAsync(client, buffer, cancellationToken); count > 0; count = buffer[count - 1] != 10 ? await ReceiveBufferedResponseAsync(client, buffer, cancellationToken) : 0)
                await memoryStream.WriteAsync(buffer, 0, count, cancellationToken);
            return memoryStream;
        }

        protected override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
        {
            RpcLogger rpcLogger = new RpcLogger(_log);
            RpcResponseMessage rpcResponseMessage;
            try
            {
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(ConnectionTimeout);

                using (var pipeStream = new NamedPipeClientStream(IpcPath))
                {
                    await pipeStream.ConnectAsync(cancellationTokenSource.Token);
                    string str = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                    byte[] bytes = Encoding.UTF8.GetBytes(str);
                    rpcLogger.LogRequest(str);
                    await pipeStream.WriteAsync(bytes, 0, bytes.Length, cancellationTokenSource.Token);
                    using (MemoryStream fullResponse = await ReceiveFullResponseAsync(pipeStream, cancellationTokenSource.Token))
                    {
                        fullResponse.Position = 0L;
                        using (StreamReader streamReader = new StreamReader(fullResponse))
                        {
                            using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                            {
                                RpcResponseMessage responseMessage = JsonSerializer.Create(JsonSerializerSettings).Deserialize<RpcResponseMessage>(jsonTextReader);
                                rpcLogger.LogResponse(responseMessage);
                                rpcResponseMessage = responseMessage;
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                var exception = new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
                rpcLogger.LogException(exception);
                throw exception;
            }
            catch (Exception ex)
            {
                var unknownException = new RpcClientUnknownException("Error occurred when trying to send ipc requests(s): " + request.Method, ex);
                rpcLogger.LogException(unknownException);
                throw unknownException;
            }
            return rpcResponseMessage;
        }
    }
}