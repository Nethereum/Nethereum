using System.IO;

using NBitcoin.BouncyCastle.Utilities.IO;

namespace NBitcoin.BouncyCastle.Asn1
{
	public abstract class LimitedInputStream
		: BaseInputStream
	{
		protected readonly Stream _in;
		private int _limit;

		public LimitedInputStream(
			Stream inStream,
			int limit)
		{
			this._in = inStream;
			this._limit = limit;
		}

		public virtual int GetRemaining()
		{
			// TODO: maybe one day this can become more accurate
			return _limit;
		}

		protected virtual void SetParentEofDetect(bool on)
		{
		}
	}
}
