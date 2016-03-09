using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace edjCase.JsonRpc.Router
{

	/// <summary>
	/// Base exception for all rpc router exceptions
	/// </summary>
	public abstract class RpcRouterException : Exception
	{
		/// <param name="message">Error message</param>
		protected RpcRouterException(string message) : base(message)
		{
		}
	}

	/// <summary>
	/// Exception for bad configuration of the Rpc server
	/// </summary>
	//Not a request exception so it is not an `RpcException`
	public class RpcConfigurationException : RpcRouterException
	{
		/// <param name="message">Error message</param>
		public RpcConfigurationException(string message) : base(message)
		{
		}
	}
}
