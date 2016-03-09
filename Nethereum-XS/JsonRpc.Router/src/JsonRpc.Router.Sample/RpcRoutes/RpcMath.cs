using System.Threading.Tasks;

namespace edjCase.JsonRpc.Router.Sample.RpcRoutes
{
	public class RpcMath
	{
		public int Add(int a, int b)
		{
			return a + b;
		}

		public long Add(long a, long c)
		{
			return a + c;
		}

		public async Task<int> AddAsync(int a, int b)
		{
			return await Task.Run(() => a + b);
		}


		public int Add(int[] a)
		{
			return a[0] + a[1];
		}

	}
}
