using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Newtonsoft.Json.Linq;

namespace edjCase.JsonRpc.Client.Sample
{
	public class Program
	{
		public async Task Main(string[] args)
		{
			try
			{
				AuthenticationHeaderValue authHeaderValue = AuthenticationHeaderValue.Parse("Basic R2VrY3RlazpXZWxjMG1lIQ==");
				RpcClient client = new RpcClient(new Uri("http://localhost:5000/RpcApi/"), authHeaderValue);
				RpcRequest request = new RpcRequest("Id1", "CharacterCount", "Test");
				RpcResponse response = await client.SendRequestAsync(request, "Strings");

				List<RpcRequest> requests = new List<RpcRequest>
				{
					request,
					new RpcRequest("id2", "CharacterCount", "Test2"),
					new RpcRequest("id3", "CharacterCount", "Test23")
				};
				List<RpcResponse> bulkResponse = await client.SendBulkRequestAsync(requests, "Strings");

				IntegerFromSpace responseValue = response.GetResult<IntegerFromSpace>();
				if (responseValue == null)
				{
					Console.WriteLine("null");
				}
				else
				{
					Console.WriteLine(responseValue.Test);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			Console.ReadLine();
		}
	}


	public class IntegerFromSpace
	{
		public int Test { get; set; }
	}
}