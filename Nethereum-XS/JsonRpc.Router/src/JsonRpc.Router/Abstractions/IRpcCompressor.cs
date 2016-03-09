using System.IO;
using System.Text;

namespace edjCase.JsonRpc.Router.Abstractions
{
	public interface IRpcCompressor
	{
		/// <summary>
		/// Compressor that takes the <paramref name="text"/> and puts it in the <paramref name="outputStream"/> in a compressed format
		/// </summary>
		/// <param name="outputStream">Stream to write the compressed value to</param>
		/// <param name="text">Text to compress</param>
		/// <param name="encoding">Encoding to be used when compressing</param>
		/// <param name="compressionType">Type of compression to be used when compressing</param>
		void CompressText(Stream outputStream, string text, Encoding encoding, CompressionType compressionType);
	}

	/// <summary>
	/// Types of compressions
	/// </summary>
	public enum CompressionType
	{
		Gzip,
		Deflate
	}
}
