# JsonRpc.Router
A DNX IRouter implementation for Json Rpc v2 requests for Microsoft.AspNet.Routing. (frameworks: dnx451, dnxcore50)

The requirements/specifications are all based off of the [Json Rpc 2.0 Specification](http://www.jsonrpc.org/specification)

## Installation
##### NuGet: [JsonRpc.Router](https://www.nuget.org/packages/JsonRpc.Router/)

using nuget command line:
```cs
Install-Package JsonRpc.Router
```
or for pre-release versions:
```cs
Install-Package JsonRpc.Router -Pre
```

## Usage
Create a AspNet 5/Dnx Web Application, reference this library and in the `Startup` class configure the following:

Add the dependency injected services in the `ConfigureServices` method:
```cs
public void ConfigureServices(IServiceCollection services)
{
	services.AddJsonRpc();
	//Adds default IRpcInvoker, IRpcParser, IRpcCompressor implementations to the services collection.
	//(Can be overridden by custom implementations if desired)
}
```

Add the JsonRpc router the pipeline in the `Configure` method:
```cs
public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
{
	app.UseJsonRpc(config =>
	{
		config.RoutePrefix = "RpcApi"; //(optional) changes base url from '/' to '/RpcApi/'
		config.RegisterClassToRpcRoute<RpcClass1>(); //Access RpcClass1 public methods at '/RpcApi/'
		config.RegisterClassToRpcRoute<RpcClass2>("Class2"); //Access RpcClass2 public methods at '/RpcApi/Class2'
		//Note RegisterClassToRpcRoute must be called at least once for the Api to work
	});
}
```
Examples of Rpc Classes:
```cs
//Classes can be named anything and be located anywhere in the project/solution
//The way to associate them to the api is to use the RegisterClassToRpcRoute<T> method in
//the configuration
public class RpcClass1
{
    //Accessable to api at /{OptionalRoutePrefix}/{OptionalRoute}/Add 
    //e.g. (from previous example) /RpcApi/Add or /RpcApi/Class2/Add
    //Example request using param list: {"jsonrpc": "2.0", "id": 1, "method": "Add", "params": [1,2]}
    //Example request using param map: {"jsonrpc": "2.0", "id": 1, "method": "Add", "params": {"a": 1, "b": 2}}
    //Example response from a=1, b=2: {"jsonrpc", "2.0", "id": 1, "result": 3}
    public int Add(int a, int b)
    {
        return a + b;
    }
    
    //This method would use the same request as Add(int a, int b) (except method would be 'AddAsync') 
    //and would respond with the same response
    public async Task<int> AddAsync(int a, int b)
    {
        //async adding here
    }
    
    //Can't be called/will return MethodNotFound because it is private. Same with all non-public/static methods.
    private void Hidden1()
    {
    }
}
```
Any method in the registered class that is a public instance method will be accessable through the Json Rpc Api.

Bulk requests are supported (as specificed in JsonRpc 2.0 docs) and will all be run asynchronously. The responses may be in a different order than the requests.

On specifics on how to create requests and what to expect from responses, use the [Json Rpc 2.0 Specification](http://www.jsonrpc.org/specification).

## Contributions

Contributions welcome. Fork as much as you want. All pull requests will be considered.

Best way to develop is to use Visual Studio 2015+ or Visual Studio Code on other platforms besides windows.

Also the correct dnx runtime has to be installed if visual studio does not automatically do that for you. 
Information on that can be found at the [Asp.Net Repo](https://github.com/aspnet/Home).

Note: I am picky about styling/readability of the code. Try to keep it similar to the current format. 

## Feedback
If you do not want to contribute directly, feel free to do bug/feature requests through github or send me and email [Gekctek@Gmail.com](mailto:Gekctek@Gmail.com)

### Todos

 - Better sample app
 - Performance testing
 - Keep up to date with latest aspnet beta

License
----
[MIT](https://raw.githubusercontent.com/Gekctek/JsonRpc.Router/master/LICENSE)
