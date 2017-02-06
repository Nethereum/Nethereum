using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.BouncyCastle.Security
{
	public class SecureRandom : Random
	{
		public SecureRandom()
		{

		}

		public static byte[] GetNextBytes(SecureRandom random, int p)
		{
			throw new NotImplementedException();
		}

		public byte NextInt()
		{
			throw new NotImplementedException();
		}

		public void NextBytes(byte[] cekBlock, int p1, int p2)
		{
			throw new NotImplementedException();
		}
	}
}
