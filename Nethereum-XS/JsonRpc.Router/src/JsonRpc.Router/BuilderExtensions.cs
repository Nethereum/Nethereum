using System;
using edjCase.JsonRpc.Core;
using edjCase.JsonRpc.Router;
using edjCase.JsonRpc.Router.Abstractions;
using edjCase.JsonRpc.Router.Defaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNet.Builder
{
	/// <summary>
	/// Extension class to add JsonRpc router to Asp.Net pipeline
	/// </summary>
	public static class BuilderExtensions
	{
		/// <summary>
		/// Extension method to use the JsonRpc router in the Asp.Net pipeline
		/// </summary>
		/// <param name="app"><see cref="IApplicationBuilder"/> that is supplied by Asp.Net</param>
		/// <param name="configureRouter">Action to configure the router properties</param>
		/// <returns><see cref="IApplicationBuilder"/> that includes the Basic auth middleware</returns>
		public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder app, Action<RpcRouterConfiguration> configureRouter)
		{
			if (app == null)
			{
				throw new ArgumentNullException(nameof(app));
			}
			if (configureRouter == null)
			{
				throw new ArgumentNullException(nameof(configureRouter));
			}

			RpcRouterConfiguration configuration = new RpcRouterConfiguration();
			configureRouter.Invoke(configuration);
			if (configuration.Routes.Count < 1)
			{
				throw new RpcConfigurationException("At least on class/route must be configured for router to work.");
			}

			IRpcInvoker rpcInvoker = app.ApplicationServices.GetRequiredService<IRpcInvoker>();
			IRpcParser rpcParser = app.ApplicationServices.GetRequiredService<IRpcParser>();
			IRpcCompressor rpcCompressor = app.ApplicationServices.GetRequiredService<IRpcCompressor>();
			ILoggerFactory loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
			ILogger logger = loggerFactory?.CreateLogger<RpcRouter>();
			app.UseRouter(new RpcRouter(configuration, rpcInvoker, rpcParser, rpcCompressor, logger));
			return app;
		}

		/// <summary>
		/// Extension method to add the JsonRpc router services to the IoC container
		/// </summary>
		/// <param name="serviceCollection">IoC serivce container to register JsonRpc dependencies</param>
		/// <returns>IoC service container</returns>
		public static IServiceCollection AddJsonRpc(this IServiceCollection serviceCollection)
		{
			return serviceCollection
				.AddSingleton<IRpcInvoker, DefaultRpcInvoker>(sp =>
				{
					ILoggerFactory loggerrFactory = sp.GetService<ILoggerFactory>();
					ILogger logger = loggerrFactory?.CreateLogger<DefaultRpcInvoker>();
					return new DefaultRpcInvoker(logger);
				})
				.AddSingleton<IRpcParser, DefaultRpcParser>(sp =>
				{
					ILoggerFactory loggerrFactory = sp.GetService<ILoggerFactory>();
					ILogger logger = loggerrFactory?.CreateLogger<DefaultRpcParser>();
					return new DefaultRpcParser(logger);
				})
				.AddSingleton<IRpcCompressor, DefaultRpcCompressor>(sp =>
				{
					ILoggerFactory loggerrFactory = sp.GetService<ILoggerFactory>();
					ILogger logger = loggerrFactory?.CreateLogger<DefaultRpcCompressor>();
					return new DefaultRpcCompressor(logger);
				});
		}
	}
}