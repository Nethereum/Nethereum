using System;

namespace NBitcoin.BouncyCastle.Crypto
{
	/**
     * this exception is thrown whenever we find something we don't expect in a
     * message.
     */
	public class InvalidCipherTextException
		: CryptoException
	{
		/**
		* base constructor.
		*/
		public InvalidCipherTextException()
		{
		}

		/**
         * create a InvalidCipherTextException with the given message.
         *
         * @param message the message to be carried with the exception.
         */
		public InvalidCipherTextException(
			string message)
			: base(message)
		{
		}

		public InvalidCipherTextException(
			string message,
			Exception exception)
			: base(message, exception)
		{
		}
	}
}
