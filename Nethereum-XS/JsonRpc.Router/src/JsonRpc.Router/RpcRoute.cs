using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace edjCase.JsonRpc.Router
{
	/// <summary>
	/// A url route and its corresponding registered classes containing Rpc methods
	/// </summary>
	public class RpcRoute
	{
		/// <summary>
		/// Name of the route
		/// </summary>
		public string Name { get; }
		/// <summary>
		/// Classes registered in this route for api use
		/// </summary>
		private List<Type> types { get; } = new List<Type>();
		
		/// <param name="name">Optional name for the route</param>
		public RpcRoute(string name = null)
		{
			this.Name = name;
		}

		/// <summary>
		/// Registers a class type to this route to allow its methods to be used in the Rpc api
		/// </summary>
		/// <typeparam name="T">Class type to register under this route</typeparam>
		/// <returns>True if the class was added to the registered classes, False if it already is registered</returns>
		public bool AddClass<T>()
		{
			Type type = typeof (T);
			if (this.types.Any(t => t == type))
			{
				return false;
			}
			this.types.Add(type);
			return true;
		}

		/// <summary>
		/// Returns the list of classes registered in this route
		/// </summary>
		/// <returns>List of classes registered in this route</returns>
		public List<Type> GetClasses()
		{
			return new List<Type>(this.types);
		} 
	}

	/// <summary>
	/// Collection of Rpc routes
	/// </summary>
	public class RpcRouteCollection : ICollection<RpcRoute>
	{
		/// <summary>
		/// List of the Rpc routes
		/// </summary>
		private List<RpcRoute> routeList { get; } = new List<RpcRoute>();
		
		/// <summary>
		/// Count of the Rpc routes
		/// </summary>
		public int Count => this.routeList.Count;

		/// <summary>
		/// Gets if the collection is readonly
		/// </summary>
		public bool IsReadOnly => false;

		/// <summary>
		/// Prefix for all the routes
		/// </summary>
		public string RoutePrefix { get; set; }

		/// <param name="routePrefix">Optional prefix for all the routes</param>
		public RpcRouteCollection(string routePrefix = null)
		{
			this.RoutePrefix = routePrefix;
		}

		/// <summary>
		/// Returns the route with the given name
		/// </summary>
		/// <param name="routeName">Name of route to retrieve</param>
		/// <returns>Rpc route with the same name (case insensitive), null if not found</returns>
		public RpcRoute GetByName(string routeName)
		{
			if (string.IsNullOrWhiteSpace(routeName))
			{
				return this.routeList.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.Name));
			}
			return this.routeList.FirstOrDefault(s => string.Equals(s.Name, routeName, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Adds the given route to the collection
		/// </summary>
		/// <param name="route">Rpc route</param>
		/// <exception cref="ArgumentException">Thrown when route with the same name already exists in the collection</exception>
		public void Add(RpcRoute route)
		{
			if(route == null)
			{
				throw new ArgumentNullException(nameof(route));
			}
			RpcRoute duplicateRoute = this.GetByName(route.Name);
			if(duplicateRoute != null)
			{
				throw new ArgumentException($"Route with the name '{route.Name}' already exists");
			}
			this.routeList.Add(route);
		} 

		/// <summary>
		/// Clears the collection of all routes
		/// </summary>
		public void Clear()
		{
			this.routeList.Clear();
		}

		/// <summary>
		/// Returns if the collection contains the given route
		/// </summary>
		/// <param name="route">Rpc route</param>
		/// <returns>True if the collection contains the given route, otherwise False</returns>
		public bool Contains(RpcRoute route)
		{
			return this.routeList.Contains(route);
		}

		/// <summary>
		/// Copies the route array to the collection
		/// </summary>
		/// <param name="array">Rpc routes</param>
		/// <param name="arrayIndex">Index to start copying from</param>
		public void CopyTo(RpcRoute[] array, int arrayIndex)
		{
			this.routeList.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Removes the Rpc route from the collection if it exists
		/// </summary>
		/// <param name="route">Rpc route</param>
		/// <returns>True if the route was removed from the collection, otherwise False</returns>
		public bool Remove(RpcRoute route)
		{
			return this.routeList.Remove(route);
		}

		/// <summary>
		/// Returns an enumerator for the collection
		/// </summary>
		/// <returns>An enumerator for the collection</returns>
		public IEnumerator<RpcRoute> GetEnumerator()
		{
			return this.routeList.GetEnumerator();
		}

		/// <summary>
		/// Returns an enumerator for the collection
		/// </summary>
		/// <returns>An enumerator for the collection</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.routeList.GetEnumerator();
		}
	}
}
