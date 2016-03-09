using System;
using System.Linq;

namespace edjCase.JsonRpc.Router
{
	/// <summary>
	/// Represents the url path for Rpc routing purposes
	/// </summary>
	public struct RpcPath : IEquatable<RpcPath>
	{
		/// <summary>
		/// Default/Empty path
		/// </summary>
		public static RpcPath Default => new RpcPath();

		/// <summary>
		/// Path components split on forward slashes
		/// </summary>
		private string[] components { get; }
		
		/// <param name="path">Url/route path</param>
		private RpcPath(string path)
		{
			this.components = !string.IsNullOrWhiteSpace(path)
				? path.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
				: new string[0];
		}
		
		/// <param name="components">Uri components for the path</param>
		private RpcPath(string[] components)
		{
			if (components == null)
			{
				throw new ArgumentNullException(nameof(components));
			}
			this.components = components;
		}

		public static bool operator ==(RpcPath path1, RpcPath path2)
		{
			return path1.Equals(path2);
		}

		public static bool operator !=(RpcPath path1, RpcPath path2)
		{
			return !path1.Equals(path2);
		}

		public bool Equals(RpcPath other)
		{
			if (other.components == null)
			{
				return this.components == null;
			}
			if (other.components.Count() != this.components.Count())
			{
				return false;
			}
			for (int i = 0; i < this.components.Length; i++)
			{
				string component = this.components[i];
				string otherComponent = other.components[i];
				if (!string.Equals(component, otherComponent, StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
			}
			return true;
		}
		

		public override bool Equals(object obj)
		{
			if (obj is RpcPath)
			{
				return this.Equals((RpcPath) obj);
			}
			return false;
		}

		public override int GetHashCode()
		{
			int hash = 1337;
			if (this.components == null)
			{
				return 0;
			}
			foreach (string component in this.components)
			{
				hash = (hash * 7) + component.GetHashCode();
			}
			return hash;
		}

		/// <summary>
		/// Creates a <see cref="RpcPath"/> based on the string form of the path
		/// </summary>
		/// <param name="path">Uri/route path</param>
		/// <returns>Rpc path based on the path string</returns>
		public static RpcPath Parse(string path)
		{
			return new RpcPath(path);
		}

		/// <summary>
		/// Merges the two paths to create a new Rpc path that is the combination of the two
		/// </summary>
		/// <param name="other">Other path to add to the end of the current path</param>
		/// <returns>A new path that is the combination of the two paths</returns>
		public RpcPath Add(RpcPath other)
		{
			int componentCount = this.components.Length + other.components.Length;
			string[] newComponents = new string[componentCount];
			this.components.CopyTo(newComponents, 0);
			other.components.CopyTo(newComponents, this.components.Length);
			return new RpcPath(newComponents);
		}
	}
}
