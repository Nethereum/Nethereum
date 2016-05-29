using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.JsonRpc.Client;
using Nethereum.Maker.ERC20Token;
using Nethereum.Web3;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SimpleTests.Issues
{
    [TestClass]

    public class IssueRaymens
    {

        private string abi = @"[{
    ""anonymous"": false,
    ""inputs"": [
      {
        ""indexed"": false,
        ""name"": ""newOwner"",
        ""type"": ""address""
      }
    ],
    ""name"": ""OwnerAdded"",
    ""type"": ""event""
  }]";


        [TestMethod]
        public void Test()
        {
            var client = new IpcClient("geth.ipc");
            var web3 = new Web3(client);
            //web3 = new Web3("http://localhost:8545");
            string address = "0xbb7e97e5670d7475437943a1b314e661d7a9fa2a";
            var makerService = new MakerTokenRegistryService(web3, "0x877c5369c747d24d9023c88c1aed1724f1993efe");
            var tokenService =  makerService.GetEthTokenServiceAsync("MKR").Result;
            var balance =  tokenService.GetBalanceOfAsync<BigInteger>(address).Result;


            var contract = web3.Eth.GetContract(abi, "0xa3969327661Ad9632638b8fe8d5dEF6ceFd94738");
            var e = contract.GetEvent("OwnerAdded");
            var filterId = e.CreateFilterAsync(new Nethereum.RPC.Eth.DTOs.BlockParameter(300000)).Result;
            var changes = e.GetAllChanges<OwnerAdded>(filterId).Result;
        }
// changes.Length == 0

public class OwnerAdded
        {
            [Parameter("address", "newOwner", 1, false)]
            public string NewOwner { get; set; }
        }
    }
        

    }

public class IpcClient : IClient, IDisposable   
{
    public JsonSerializerSettings JsonSerializerSettings { get; set; }

    private string ipcPath;

    private object lockingObject = new object();

    private NamedPipeClientStream pipeClient;
    private NamedPipeClientStream GetPipeClient()
    {
        lock (lockingObject)
        {
            try
            {
                if (pipeClient == null || !pipeClient.IsConnected)
                {
                    pipeClient = new NamedPipeClientStream(ipcPath);
                    pipeClient.Connect();
                }
            }
            catch
            {
                //Connection error we want to allow to retry.
                pipeClient = null;
                throw;
            }
        }

        return pipeClient;
    }


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
            try
            {

                var pipeClient = GetPipeClient();

                string rpcRequestJson = JsonConvert.SerializeObject(request, this.JsonSerializerSettings);
                byte[] requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);
                await pipeClient.WriteAsync(requestBytes, 0, requestBytes.Length);

                var responseBytes = await ReadResponseStream(pipeClient);

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
                throw new RpcClientUnknownException("Error occurred when trying to send ipc requests(s)", ex);
            }
        
    }

    #region IDisposable Support
    private bool disposedValue = false; 

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (pipeClient != null)
                {
                    pipeClient.Close();
                    pipeClient.Dispose();
                }
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }
    #endregion

}



