using Nethereum.Generators.Core;
using Xunit;

namespace Nethereum.Generators.UnitTests.Tests.Core
{
    public class CodeGenLanguageExtTests
    {

        [Theory]
        [InlineData("x", "x", true)]
        [InlineData("x", "X", true)]
        [InlineData("y", "X", false)]
        public void StringComparerIgnoreCase(string x, string y, bool shouldEqual)
        {
            //StringComparerIgnoreCase is a workaround class.  It's only necessary because DuoCode doesnt support StringComparer
            var comparer = new CodeGenLanguageExt.StringComparerIgnoreCase();
            if(shouldEqual)
                Assert.True(comparer.Equals(x, y));
            else
                Assert.False(comparer.Equals(x, y));
        }
    }
}
