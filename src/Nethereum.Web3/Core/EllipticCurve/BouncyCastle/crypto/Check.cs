namespace NBitcoin.BouncyCastle.Crypto
{
	public class Check
	{
		public static void DataLength(bool condition, string msg)
		{
			if(condition)
				throw new DataLengthException(msg);
		}

		public static void DataLength(byte[] buf, int off, int len, string msg)
		{
			if(off + len > buf.Length)
				throw new DataLengthException(msg);
		}

		public static void OutputLength(byte[] buf, int off, int len, string msg)
		{
			if(off + len > buf.Length)
				throw new OutputLengthException(msg);
		}
	}
}
