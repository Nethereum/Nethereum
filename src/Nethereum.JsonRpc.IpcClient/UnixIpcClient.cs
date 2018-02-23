using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Sockets;
using Nethereum.JsonRpc.Client;

namespace Nethereum.JsonRpc.IpcClient
{
    public class UnixIpcClient : IpcClientBase
    {
        public UnixIpcClient(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null) : base(ipcPath, jsonSerializerSettings)
        {

        }

        public int ReceiveBufferedResponse(Socket client, byte[] buffer)
        {
#if NET461
            int bytesRead = 0;
            if (Task.Run(() => 
                    bytesRead = client.Receive(buffer, SocketFlags.None)
                ).Wait(2000))
            {
                return bytesRead;
            }
            else
            {
                return bytesRead;
            }
#else
             int bytesRead = 0;
            if (Task.Run(async () => 
                    bytesRead = await client.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None)
                ).Wait(2000))
            {
                return bytesRead;
            }
            else
            {
                return bytesRead;
            }
#endif
        }

        public MemoryStream ReceiveFullResponse(Socket client)
        {
            var readBufferSize = 512;
            var memoryStream = new MemoryStream();

            int bytesRead = 0;
            byte[] buffer = new byte[readBufferSize];
            bytesRead = ReceiveBufferedResponse(client, buffer);
            while (bytesRead > 0)
            {
                memoryStream.Write(buffer, 0, bytesRead);
                var lastByte = buffer[bytesRead - 1];

                if (lastByte == 10)  //return signalled with a line feed
                {
                    bytesRead = 0;
                }
                else
                {
                    bytesRead = ReceiveBufferedResponse(client, buffer);
                }
            }
            return memoryStream;
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
                    client.Connect(endPoint);
                    client.SendBufferSize = requestBytes.Length;
#if NET461
                    var val = client.Send(requestBytes, SocketFlags.None);
#else
                    var val = await client.SendAsync(new ArraySegment<byte>(requestBytes, 0, requestBytes.Length), SocketFlags.None).ConfigureAwait(false);
#endif
                    using (var memoryStream = ReceiveFullResponse(client))
                    {
                        memoryStream.Position = 0;
                        using (StreamReader streamReader = new StreamReader(memoryStream))
                        using (JsonTextReader reader = new JsonTextReader(streamReader))
                        {
                            var serializer = JsonSerializer.Create(JsonSerializerSettings);
                            return serializer.Deserialize<TResponse>(reader);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                throw new RpcClientUnknownException("Error occurred when trying to send ipc requests(s)", ex);
            }
        }

#region IDisposable Support

        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
            }
        }

#endregion
    }
}
