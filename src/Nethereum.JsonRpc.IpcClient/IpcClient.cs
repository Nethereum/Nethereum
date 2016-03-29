using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;

namespace Nethereum.JsonRpc.IpcClient
{
    public class IpcClient : IClient
    {
        public JsonSerializerSettings JsonSerializerSettings { get; set; }

        private string ipcPath;
   
        public IpcClient(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null)
        {
            this.ipcPath = ipcPath;
            this.JsonSerializerSettings = jsonSerializerSettings;
        }

        public async Task<RpcResponse> SendRequestAsync(RpcRequest request, string route = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            return await this.SendAsync<RpcRequest, RpcResponse>(request);
        }

        public Task<RpcResponse> SendRequestAsync(string method, string route = null, params object[] paramList)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                throw new ArgumentNullException(nameof(method));
            }
            RpcRequest request = new RpcRequest(Guid.NewGuid().ToString(), method, paramList);
            return this.SendRequestAsync(request);
        }

        private async Task<byte[]> ReadResponseStream(NamedPipeClientStream pipeClientStream)
        {
            var buffer = new byte[pipeClientStream.InBufferSize];
            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    var read = await pipeClientStream.ReadAsync(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, read);
                    if (read < pipeClientStream.InBufferSize)
                    {
                        return ms.ToArray();
                    }
                }
            }
        }

        private async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        {
            using (var pipeClient = new NamedPipeClientStream(ipcPath))
            {
                try
                {
                    
                    pipeClient.Connect();

                    string rpcRequestJson = JsonConvert.SerializeObject(request, this.JsonSerializerSettings);
                    byte[] requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);
                    await pipeClient.WriteAsync(requestBytes, 0, requestBytes.Length);

                    var responseBytes = await ReadResponseStream(pipeClient);

                    pipeClient.Close();

                    string responseJson = Encoding.UTF8.GetString(responseBytes);

                    try
                    {
                        return JsonConvert.DeserializeObject<TResponse>(responseJson, this.JsonSerializerSettings);
                    }
                    catch (JsonSerializationException)
                    {
                        RpcResponse rpcResponse = JsonConvert.DeserializeObject<RpcResponse>(responseJson, this.JsonSerializerSettings);
                        if (rpcResponse == null)
                        {
                            throw new RpcClientUnknownException(
                                $"Unable to parse response from the ipc server. Response Json: {responseJson}");
                        }
                        throw rpcResponse.Error.CreateException();
                    }
            }
            catch (Exception ex) when (!(ex is RpcClientException) && !(ex is RpcException))
            {
                throw new RpcClientUnknownException("Error occurred when trying to send rpc requests(s)", ex);
            }
            }
        }

    }
}


