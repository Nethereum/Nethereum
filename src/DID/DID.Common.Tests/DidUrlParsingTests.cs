using System;
using System.Text.RegularExpressions;
using Xunit;

namespace Did.Common.Tests
{
    public class DidUrlParsingTests
    {
        [Fact]
        public void Should_ParseIdCharIncludingDot_UnderLine_Line()
        {
            var matchChars = new[] { 'a', '1', 'z', 'Z', '_', '.', '-'};

            string pattern = DidUrlParser.ID_CHAR;
            var rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            foreach (var matchChar in matchChars)
            {
                var match = rgx.IsMatch(matchChar.ToString());
                Assert.True(match);
            }

            var unmatchedChars = new[] { '#', '?', ':', '%' };
            foreach (var unmatchChar in unmatchedChars)
            {
                var match = rgx.IsMatch(unmatchChar.ToString());
                Assert.False(match);
            }
        }

        [Fact]
        public void Should_ParseMethodNameIncludingUnderLine()
        {
            var matchChars = new[] { 'a', '1', 'z', 'Z', '_' };

            string pattern = DidUrlParser.METHOD_NAME;
            var rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            foreach (var matchChar in matchChars)
            {
                var match = rgx.IsMatch(matchChar.ToString());
                Assert.True(match);
            }

            var unmatchedChars = new[] { '#', '?', '.', '-', ':', '%' };
            foreach (var unmatchChar in unmatchedChars)
            {
                var match = rgx.IsMatch(unmatchChar.ToString());
                Assert.False(match);
            }
        }

        [Fact]
        public void Should_ParseMethodId()
        {
            string pattern = DidUrlParser.METHOD_NAME;
            var rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = rgx.IsMatch("ethr:0xb9c5714089478a327f09197987f16f9e5d936e8a");
            Assert.True(match);
            var x = rgx.Match("ethr:0xb9c5714089478a327f09197987f16f9e5d936e8a");
            var y = x.Value;
        }

        [Fact]
        public void Should_ParseParamChar_Including_Underscore_Dot_Line_Semicolon_Percentage()
        {
            var matchChars = new[]{ 'a', '1', 'z', 'Z', '_', '.', '-', ':', '%' };
            
            string pattern = DidUrlParser.PARAM_CHAR;
            var rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            foreach (var matchChar in matchChars)
            {
                var match = rgx.IsMatch(matchChar.ToString());
                Assert.True(match);
            }

            var unmatchedChars = new[] { '#', '?'};
            foreach (var unmatchChar in unmatchedChars)
            {
                var match = rgx.IsMatch(unmatchChar.ToString());
                Assert.False(match);
            }
        }


        [Fact]
        public void Should_ParseParam()
        {
            string pattern = DidUrlParser.PARAM;
            var rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = rgx.IsMatch(";service=agent");
            var x = rgx.Matches(";service=agent");
            var paramNames = x[0].Groups["ParamName"];
            Assert.Equal("service", paramNames.Captures[0].Value);
            var paramValues = x[0].Groups["ParamValue"];
            Assert.Equal("agent", paramValues.Captures[0].Value);
        }

        [Fact]
        public void Should_ParseParams()
        {
            string pattern = DidUrlParser.PARAMS;
            var rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = rgx.IsMatch(";service=agent;foo:bar=high");
            Assert.True(match);
            var x = rgx.Matches(";service=agent;foo:bar=high");
            var paramNames = x[0].Groups["ParamName"];
            Assert.Equal("service", paramNames.Captures[0].Value);
            Assert.Equal("foo:bar", paramNames.Captures[1].Value);
            var paramValues = x[0].Groups["ParamValue"];
            Assert.Equal("agent", paramValues.Captures[0].Value);
            Assert.Equal("high", paramValues.Captures[1].Value);
        }

        [Fact]
        public void ShouldParseAll()
        {
            string pattern = DidUrlParser.DID_URL;
            var rgx = new Regex(pattern, RegexOptions.IgnoreCase);

            var x = rgx.Matches("did:example:test:21tDAKCERh95uGgKbJNHYp;service=agent;foo:bar=high/some/path?foo=bar#key1");
            
            var paramNames = x[0].Groups["ParamName"];
            Assert.Equal("service", paramNames.Captures[0].Value);
            Assert.Equal("foo:bar", paramNames.Captures[1].Value);
            var paramValues = x[0].Groups["ParamValue"];
            Assert.Equal("agent", paramValues.Captures[0].Value);
            Assert.Equal("high", paramValues.Captures[1].Value);

            var methodName = x[0].Groups["MethodName"];
            Assert.Equal("example", methodName.Captures[0].Value);

            var methodId = x[0].Groups["MethodId"];
            Assert.Equal("test:21tDAKCERh95uGgKbJNHYp", methodId.Captures[0].Value);
            
            var path = x[0].Groups["Path"];
            Assert.Equal("/some/path", path.Captures[0].Value);
            
            var fragment = x[0].Groups["Fragment"];
            Assert.Equal("#key1", fragment.Captures[0].Value);

            var query = x[0].Groups["Query"];
            Assert.Equal("?foo=bar", query.Captures[0].Value);  
        }


        [Fact]
        public void ShouldParseDidUrlFull()
        {
            var didUrl = DidUrlParser.Parse("did:example:test:21tDAKCERh95uGgKbJNHYp;service=agent;foo:bar=high/some/path?foo=bar#key1");

            Assert.Equal("agent", didUrl.Params["service"]);
            Assert.Equal("high", didUrl.Params["foo:bar"]);
            Assert.Equal("example", didUrl.Method);
            Assert.Equal("test:21tDAKCERh95uGgKbJNHYp", didUrl.Id);
            Assert.Equal("/some/path", didUrl.Path);
            Assert.Equal("key1", didUrl.Fragment);
            Assert.Equal("foo=bar", didUrl.Query);
            Assert.Equal("did:example:test:21tDAKCERh95uGgKbJNHYp;service=agent;foo:bar=high/some/path?foo=bar#key1", didUrl.Url);
        }

        [Fact]
        public void ShouldParseDidUrlParamsAndQuery()
        {
            var didUrl = DidUrlParser.Parse("did:example:test:21tDAKCERh95uGgKbJNHYp;service=agent;foo:bar=high?foo=bar");

            Assert.Equal("agent", didUrl.Params["service"]);
            Assert.Equal("high", didUrl.Params["foo:bar"]);
            Assert.Equal("example", didUrl.Method);
            Assert.Equal("test:21tDAKCERh95uGgKbJNHYp", didUrl.Id);
            Assert.Equal("foo=bar", didUrl.Query);
            Assert.Equal("did:example:test:21tDAKCERh95uGgKbJNHYp", didUrl.Did);
            Assert.Equal("did:example:test:21tDAKCERh95uGgKbJNHYp;service=agent;foo:bar=high?foo=bar", didUrl.Url);
        }


        [Fact]
        public void ShouldParseDidUrlPath()
        {
            var url = "did:example:test:21tDAKCERh95uGgKbJNHYp/some/path";
            var didUrl = DidUrlParser.Parse(url);

            Assert.Equal("example", didUrl.Method);
            Assert.Equal("test:21tDAKCERh95uGgKbJNHYp", didUrl.Id);
            Assert.Equal("/some/path", didUrl.Path);
            Assert.Equal("did:example:test:21tDAKCERh95uGgKbJNHYp", didUrl.Did);
            Assert.Equal(url, didUrl.Url);
        }
        [Fact]
        public void ShouldParseDidUrlFragment()
        {
            var url = "did:example:test:21tDAKCERh95uGgKbJNHYp#key1=123";
            var didUrl = DidUrlParser.Parse(url);

            Assert.Equal("example", didUrl.Method);
            Assert.Equal("test:21tDAKCERh95uGgKbJNHYp", didUrl.Id);
            Assert.Equal("did:example:test:21tDAKCERh95uGgKbJNHYp", didUrl.Did);
            Assert.Equal(url, didUrl.Url);
            Assert.Equal("key1=123", didUrl.Fragment);
        }

    }
}
