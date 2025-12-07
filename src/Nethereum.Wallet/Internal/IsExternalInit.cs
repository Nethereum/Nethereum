#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Provides support for record types and init-only setters when targeting frameworks
    /// that do not include the built-in <c>IsExternalInit</c> type.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
