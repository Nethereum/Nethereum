using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using edjCase.JsonRpc.Router.Abstractions;
using edjCase.JsonRpc.Router.Defaults;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace edjCase.JsonRpc.Router.Tests
{
	public class InvokerTests
	{
		[Fact]
		public void InvokeRequest_StringParam_ParseAsGuidType()
		{
			Guid randomGuid = Guid.NewGuid();
			RpcRequest stringRequest = new RpcRequest("1", "GuidTypeMethod", randomGuid.ToString());

			RpcRoute route = new RpcRoute();
			route.AddClass<TestRouteClass>();
			IRpcInvoker invoker = new DefaultRpcInvoker();
			RpcResponse stringResponse = invoker.InvokeRequest(stringRequest, route);

			
			Assert.Equal(stringResponse.Result, randomGuid);
		}

		[Fact]
		public void InvokeRequest_AmbiguousRequest_ErrorResponse()
		{
			RpcRequest stringRequest = new RpcRequest("1", "AmbiguousMethod", 1);
			RpcRoute route = new RpcRoute();
			route.AddClass<TestRouteClass>();
			IRpcInvoker invoker = new DefaultRpcInvoker();
			RpcResponse response = invoker.InvokeRequest(stringRequest, route);
			
			Assert.NotNull(response.Error);
			Assert.Equal(response.Error.Code, RpcErrorCode.AmbiguousMethod);
		}

		[Fact]
		public void InvokeRequest_AsyncMethod_Valid()
		{
			RpcRequest stringRequest = new RpcRequest("1", "AddAsync", 1, 1);
			RpcRoute route = new RpcRoute();
			route.AddClass<TestRouteClass>();
			IRpcInvoker invoker = new DefaultRpcInvoker();

			RpcResponse response = invoker.InvokeRequest(stringRequest, route);

			RpcResponse resultResponse = Assert.IsType<RpcResponse>(response);
			Assert.NotNull(resultResponse.Result);
			Assert.Equal(resultResponse.Result, 2);
		}

		[Fact]
		public void InvokeRequest_Int64RequestParam_ConvertToInt32Param()
		{
			RpcRequest stringRequest = new RpcRequest("1", "IntParameter", (long)1);
			RpcRoute route = new RpcRoute();
			route.AddClass<TestRouteClass>();
			IRpcInvoker invoker = new DefaultRpcInvoker();

			RpcResponse response = invoker.InvokeRequest(stringRequest, route);

			RpcResponse resultResponse = Assert.IsType<RpcResponse>(response);
			Assert.NotNull(resultResponse.Result);
			Assert.IsType<int>(resultResponse.Result);
			Assert.Equal(resultResponse.Result, 1);
		}

		[Fact]
		public void InvokeRequest_ServiceProvider_Pass()
		{
			RpcRequest stringRequest = new RpcRequest("1", "Test");
			RpcRoute route = new RpcRoute();
			route.AddClass<TestIoCRouteClass>();
			IRpcInvoker invoker = new DefaultRpcInvoker();
			IServiceCollection serviceCollection = new ServiceCollection();
			serviceCollection.AddScoped<TestInjectionClass>();
			serviceCollection.AddScoped<TestIoCRouteClass>();
			IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
			RpcResponse response = invoker.InvokeRequest(stringRequest, route, serviceProvider);

			RpcResponse resultResponse = Assert.IsType<RpcResponse>(response);
			Assert.NotNull(resultResponse.Result);
			Assert.IsType<int>(resultResponse.Result);
			Assert.Equal(resultResponse.Result, 1);
		}
	}

	public class TestRouteClass
	{
		public Guid GuidTypeMethod(Guid guid)
		{
			return guid;
		}

		public int AmbiguousMethod(int a)
		{
			return a;
		}

		public long AmbiguousMethod(long a)
		{
			return a;
		}

		public async Task<int> AddAsync(int a, int b)
		{
			return await Task.Run(() => a + b);
		}

		public int IntParameter(int a)
		{
			return a;
		}
	}

	public class TestIoCRouteClass
	{
		private TestInjectionClass test { get; }
		public TestIoCRouteClass(TestInjectionClass test)
		{
			this.test = test;
		}

		public int Test()
		{
			return 1;
		}
	}
	public class TestInjectionClass
	{

	}
}
