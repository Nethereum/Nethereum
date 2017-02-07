using NBitcoin.BouncyCastle.Security;


namespace NBitcoin.BouncyCastle.Crypto.Paddings
{
	/**
     * Block cipher padders are expected to conform to this interface
     */
	internal interface IBlockCipherPadding
	{
		/**
         * Initialise the padder.
         *
         * @param param parameters, if any required.
         */
		void Init(SecureRandom random);
		//throws ArgumentException;

		/**
         * Return the name of the algorithm the cipher implements.
         *
         * @return the name of the algorithm the cipher implements.
         */
		string PaddingName
		{
			get;
		}

		/**
         * add the pad bytes to the passed in block, returning the
         * number of bytes added.
         */
		int AddPadding(byte[] input, int inOff);

		/**
         * return the number of pad bytes present in the block.
         * @exception InvalidCipherTextException if the padding is badly formed
         * or invalid.
         */
		int PadCount(byte[] input);
		//throws InvalidCipherTextException;
	}

}
