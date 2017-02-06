using System;
using System.IO;

namespace NBitcoin.BouncyCastle.Utilities.IO
{
	public class StreamOverflowException
		: IOException
	{
		public StreamOverflowException()
			: base()
		{
		}

		public StreamOverflowException(
			string message)
			: base(message)
		{
		}

		public StreamOverflowException(
			string message,
			Exception exception)
			: base(message, exception)
		{
		}
	}
}
