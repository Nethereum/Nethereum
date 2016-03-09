using System;

namespace edjCase.JsonRpc.Router.Sample.RpcRoutes
{
	public class RpcCommands
	{
		public bool ValidateId(Guid id)
		{
			return id != Guid.Empty;
		}
		

		private void HiddenMethod()
		{

		}
	}
}
