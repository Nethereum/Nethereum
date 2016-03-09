using System;
using Newtonsoft.Json;

namespace edjCase.JsonRpc.Router
{
	/// <summary>
	/// Configuration data for the Rpc router
	/// </summary>
	public class RpcRouterConfiguration
	{
		/// <summary>
		/// Collection of routes configured to be allowed to be called by the client
		/// </summary>
		internal RpcRouteCollection Routes { get; }
		
		/// <summary>
		/// Json serialization settings that will be used in serialization and deserialization
		/// for rpc requests
		/// </summary>
		internal JsonSerializerSettings JsonSerializerSettings { get; private set; }

		/// <summary>
		/// The prefix for the routes
		/// </summary>
		public string RoutePrefix
		{
			get { return this.Routes.RoutePrefix; }
			set { this.Routes.RoutePrefix = value; }
		}
		
		/// <param name="routePrefix">Optional prefix for all the routes</param>
		public RpcRouterConfiguration(string routePrefix = null)
		{
			this.Routes = new RpcRouteCollection(routePrefix);
		}

		/// <summary>
		/// Registers the class's public methods to be used in the Rpc api under the optional route name
		/// </summary>
		/// <typeparam name="T">Class to open up to the Rpc api</typeparam>
		/// <param name="routeName">Optional route to put the class's Rpc methods</param>
		public void RegisterClassToRpcRoute<T>(string routeName = null)
		{
			Type type = typeof(T);
			RpcRoute route = this.Routes.GetByName(routeName);

			if (route == null)
			{
				route = new RpcRoute(routeName);
				this.Routes.Add(route);
			}

			bool uniqueClass = route.AddClass<T>();
			if (uniqueClass)
			{
				return;
			}
			string alreadyRegisteredMessage = $"Type '{type.FullName}' has already been registered " +
											$"with the Rpc router under the route '{routeName}'";
			throw new ArgumentException(alreadyRegisteredMessage);
		}

		/// <summary>
		/// Sets the json serialization settings that will be used in serialization and deserialization
		/// for rpc requests
		/// </summary>
		/// <param name="jsonSerializerSettings"></param>
		public void SetJsonSerializerSettings(JsonSerializerSettings jsonSerializerSettings)
		{
			this.JsonSerializerSettings = jsonSerializerSettings;
		}
	}
}
