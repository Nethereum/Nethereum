using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Client;
using EdjCase.JsonRpc.Core;
using Newtonsoft.Json;
using System.Net.Sockets;

namespace Nethereum.JsonRpc.IpcClient
{
    public class UnixIpcClient : IpcClientBase
    {
        public UnixIpcClient(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null) : base(ipcPath, jsonSerializerSettings)
        {

        }

        protected override async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        {
            try
            {
                var rpcRequestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                var requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);

                var endPoint = new UnixDomainSocketEndPoint(IpcPath);

                using (var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
                {
                    Console.WriteLine("Connecting");
                    client.Connect(endPoint);
                    Console.WriteLine("Connected: " + client.Connected + " Now sending");
                   
                    var val = client.Send(requestBytes, SocketFlags.None);

                    using (NetworkStream networkStream = new NetworkStream(client))
                    using (StreamReader streamReader = new StreamReader(networkStream))
                    using (JsonTextReader reader = new JsonTextReader(streamReader))
                    {
                        var serializer = new JsonSerializer();
                        return serializer.Deserialize<TResponse>(reader);
                    }
                    throw new RpcClientUnknownException(
                            $"Unable to parse response from the ipc server");
                }

            }
            catch (Exception ex) when (!(ex is RpcClientException) && !(ex is RpcException))
            {
                throw new RpcClientUnknownException("Error occurred when trying to send ipc requests(s)", ex);
            }
        }

        #region IDisposable Support

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
            }
        }

        #endregion
    }
}
