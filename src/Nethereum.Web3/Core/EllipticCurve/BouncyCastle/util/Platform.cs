using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;

namespace NBitcoin.BouncyCastle.Utilities
{
	public abstract class Platform
	{
		private static readonly CompareInfo InvariantCompareInfo = CultureInfo.InvariantCulture.CompareInfo;

		private static string GetNewLine()
		{
			return Environment.NewLine;
		}

		public static bool EqualsIgnoreCase(string a, string b)
		{
          return String.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}

		public static string GetEnvironmentVariable(
			string variable)
		{
			return null;
		}

		public static Exception CreateNotImplementedException(
			string message)
		{
			return new NotImplementedException(message);
		}

		public static System.Collections.IList CreateArrayList()
		{
			return new List<object>();
		}
		public static System.Collections.IList CreateArrayList(int capacity)
		{
			return new List<object>(capacity);
		}
		public static System.Collections.IList CreateArrayList(System.Collections.ICollection collection)
		{
			System.Collections.IList result = new List<object>(collection.Count);
			foreach(object o in collection)
			{
				result.Add(o);
			}
			return result;
		}
		public static System.Collections.IList CreateArrayList(System.Collections.IEnumerable collection)
		{
			System.Collections.IList result = new List<object>();
			foreach(object o in collection)
			{
				result.Add(o);
			}
			return result;
		}
		public static System.Collections.IDictionary CreateHashtable()
		{
			return new Dictionary<object, object>();
		}
		public static System.Collections.IDictionary CreateHashtable(int capacity)
		{
			return new Dictionary<object, object>(capacity);
		}
		public static System.Collections.IDictionary CreateHashtable(System.Collections.IDictionary dictionary)
		{
			System.Collections.IDictionary result = new Dictionary<object, object>(dictionary.Count);
			foreach(System.Collections.DictionaryEntry entry in dictionary)
			{
				result.Add(entry.Key, entry.Value);
			}
			return result;
		}
		public static string ToLowerInvariant(string s)
		{

            return s.ToLowerInvariant();
		}

		public static string ToUpperInvariant(string s)
		{
        return s.ToUpperInvariant();

		}

		public static readonly string NewLine = GetNewLine();

        public static void Dispose(IDisposable d)
        {
            d.Dispose();
        }

		public static int IndexOf(string source, string value)
		{
			return InvariantCompareInfo.IndexOf(source, value, CompareOptions.Ordinal);
		}

		public static int LastIndexOf(string source, string value)
		{
			return InvariantCompareInfo.LastIndexOf(source, value, CompareOptions.Ordinal);
		}

		public static bool StartsWith(string source, string prefix)
		{
			return InvariantCompareInfo.IsPrefix(source, prefix, CompareOptions.Ordinal);
		}

		public static bool EndsWith(string source, string suffix)
		{
			return InvariantCompareInfo.IsSuffix(source, suffix, CompareOptions.Ordinal);
		}

		public static string GetTypeName(object obj)
		{
			return obj.GetType().FullName;
		}
	}
}
