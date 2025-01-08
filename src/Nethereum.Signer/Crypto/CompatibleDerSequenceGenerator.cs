using System.IO;
using Org.BouncyCastle.Asn1;

namespace Nethereum.Signer.Crypto
{
    /// <summary>
    /// Proxy that provides compatibility between difference Bouncy Castle versions.
    /// </summary>
    public class CompatibleDerSequenceGenerator : DerSequenceGenerator
    {
        public CompatibleDerSequenceGenerator(Stream outStream) : base(outStream)
        {
        }

        public CompatibleDerSequenceGenerator(Stream outStream, int tagNo, bool isExplicit) : base(outStream, tagNo, isExplicit)
        {
        }

        // The Close method is not available in the latest Bouncy Castle version
        // It's part of the Finish method which is protected
        public void Close()
        {
            #if LATEST_BOUNCYCASTLE
            Finish();
            #else
            base.Close();
            #endif
        }
    }
}