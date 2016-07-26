using System;

using NBitcoin.BouncyCastle.Utilities;

namespace NBitcoin.BouncyCastle.Crypto.Parameters
{
	public abstract class ECKeyParameters
		: AsymmetricKeyParameter
	{
		private static readonly string[] algorithms = { "EC", "ECDSA", "ECDH", "ECDHC", "ECGOST3410", "ECMQV" };

		private readonly string algorithm;
		private readonly ECDomainParameters parameters;

		protected ECKeyParameters(
			string algorithm,
			bool isPrivate,
			ECDomainParameters parameters)
			: base(isPrivate)
		{
			if(algorithm == null)
				throw new ArgumentNullException("algorithm");
			if(parameters == null)
				throw new ArgumentNullException("parameters");

			this.algorithm = VerifyAlgorithmName(algorithm);
			this.parameters = parameters;
		}

		public string AlgorithmName
		{
			get
			{
				return algorithm;
			}
		}

		public ECDomainParameters Parameters
		{
			get
			{
				return parameters;
			}
		}

		public override bool Equals(
			object obj)
		{
			if(obj == this)
				return true;

			ECDomainParameters other = obj as ECDomainParameters;

			if(other == null)
				return false;

			return Equals(other);
		}

		protected bool Equals(
			ECKeyParameters other)
		{
			return parameters.Equals(other.parameters) && base.Equals(other);
		}

		public override int GetHashCode()
		{
			return parameters.GetHashCode() ^ base.GetHashCode();
		}

		public static string VerifyAlgorithmName(string algorithm)
		{
			string upper = Platform.ToUpperInvariant(algorithm);
			if(Array.IndexOf(algorithms, algorithm, 0, algorithms.Length) < 0)
				throw new ArgumentException("unrecognised algorithm: " + algorithm, "algorithm");
			return upper;
		}
	}
}
