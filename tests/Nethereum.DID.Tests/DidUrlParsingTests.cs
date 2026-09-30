using System;
using Xunit;

namespace Nethereum.DID.Tests
{
    public class DidUrlParsingTests
    {
        [Fact]
        public void ShouldParseSimpleDid()
        {
            var result = DidUrlParser.Parse("did:example:123456789abcdefghi");
            Assert.Equal("example", result.Method);
            Assert.Equal("123456789abcdefghi", result.Id);
            Assert.Equal("did:example:123456789abcdefghi", result.Did);
        }

        [Fact]
        public void ShouldParseDidEthrWithAddress()
        {
            var result = DidUrlParser.Parse("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8a");
            Assert.Equal("ethr", result.Method);
            Assert.Equal("0xb9c5714089478a327f09197987f16f9e5d936e8a", result.Id);
            Assert.Equal("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8a", result.Did);
        }

        [Fact]
        public void ShouldParseDidEthrWithChainId()
        {
            var result = DidUrlParser.Parse("did:ethr:1:0xb9c5714089478a327f09197987f16f9e5d936e8a");
            Assert.Equal("ethr", result.Method);
            Assert.Equal("1:0xb9c5714089478a327f09197987f16f9e5d936e8a", result.Id);
            Assert.Equal("did:ethr:1:0xb9c5714089478a327f09197987f16f9e5d936e8a", result.Did);
        }

        [Fact]
        public void ShouldParseDidWithFragment()
        {
            var result = DidUrlParser.Parse("did:example:123456789abcdefghi#keys-1");
            Assert.Equal("example", result.Method);
            Assert.Equal("123456789abcdefghi", result.Id);
            Assert.Equal("keys-1", result.Fragment);
        }

        [Fact]
        public void ShouldParseDidWithPath()
        {
            var result = DidUrlParser.Parse("did:example:123456789abcdefghi/path/to/resource");
            Assert.Equal("example", result.Method);
            Assert.Equal("123456789abcdefghi", result.Id);
            Assert.Equal("/path/to/resource", result.Path);
        }

        [Fact]
        public void ShouldParseDidWithQuery()
        {
            var result = DidUrlParser.Parse("did:example:123456789abcdefghi?query=value");
            Assert.Equal("example", result.Method);
            Assert.Equal("query=value", result.Query);
        }

        [Fact]
        public void ShouldParseDidWithParams()
        {
            var result = DidUrlParser.Parse("did:example:123456789abcdefghi;service=agent;foo=bar");
            Assert.Equal("example", result.Method);
            Assert.Equal(2, result.Params.Count);
            Assert.Equal("agent", result.Params["service"]);
            Assert.Equal("bar", result.Params["foo"]);
        }

        [Fact]
        public void ShouldParseDidWithAllComponents()
        {
            var result = DidUrlParser.Parse("did:example:123456789abcdefghi;service=agent/path?query=value#fragment");
            Assert.Equal("example", result.Method);
            Assert.Equal("123456789abcdefghi", result.Id);
            Assert.Equal("agent", result.Params["service"]);
            Assert.Equal("/path", result.Path);
            Assert.Equal("query=value", result.Query);
            Assert.Equal("fragment", result.Fragment);
        }

        [Fact]
        public void ShouldParseDidWithPctEncoding()
        {
            var result = DidUrlParser.Parse("did:example:123%20456");
            Assert.Equal("example", result.Method);
            Assert.Equal("123%20456", result.Id);
        }

        [Fact]
        public void ShouldParseDidWithColonsInId()
        {
            var result = DidUrlParser.Parse("did:example:sub1:sub2:identifier");
            Assert.Equal("example", result.Method);
            Assert.Equal("sub1:sub2:identifier", result.Id);
        }

        [Fact]
        public void ShouldParseDidEthrWithController()
        {
            var result = DidUrlParser.Parse("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8a#controller");
            Assert.Equal("ethr", result.Method);
            Assert.Equal("controller", result.Fragment);
        }

        [Fact]
        public void ShouldReturnNullFragmentWhenNone()
        {
            var result = DidUrlParser.Parse("did:example:123");
            Assert.Null(result.Fragment);
        }

        [Fact]
        public void ShouldReturnNullPathWhenNone()
        {
            var result = DidUrlParser.Parse("did:example:123");
            Assert.Null(result.Path);
        }

        [Fact]
        public void ShouldReturnNullQueryWhenNone()
        {
            var result = DidUrlParser.Parse("did:example:123");
            Assert.Null(result.Query);
        }

        [Fact]
        public void ShouldReturnEmptyParamsWhenNone()
        {
            var result = DidUrlParser.Parse("did:example:123");
            Assert.Empty(result.Params);
        }

        [Fact]
        public void ShouldThrowOnNullInput()
        {
            Assert.Throws<ArgumentException>(() => DidUrlParser.Parse(null));
        }

        [Fact]
        public void ShouldThrowOnEmptyInput()
        {
            Assert.Throws<ArgumentException>(() => DidUrlParser.Parse(""));
        }

        [Fact]
        public void ShouldThrowOnInvalidDid()
        {
            Assert.Throws<FormatException>(() => DidUrlParser.Parse("not-a-did"));
        }

        [Fact]
        public void ShouldThrowOnMissingMethod()
        {
            Assert.Throws<FormatException>(() => DidUrlParser.Parse("did::123"));
        }

        [Fact]
        public void TryParseShouldReturnFalseForInvalidInput()
        {
            DidUrl result;
            Assert.False(DidUrlParser.TryParse("not-a-did", out result));
            Assert.Null(result);
        }

        [Fact]
        public void TryParseShouldReturnTrueForValidInput()
        {
            DidUrl result;
            Assert.True(DidUrlParser.TryParse("did:example:123", out result));
            Assert.NotNull(result);
            Assert.Equal("example", result.Method);
        }

        [Fact]
        public void TryParseShouldReturnFalseForNull()
        {
            DidUrl result;
            Assert.False(DidUrlParser.TryParse(null, out result));
            Assert.Null(result);
        }

        [Fact]
        public void ShouldPreserveOriginalUrl()
        {
            var url = "did:example:123456789abcdefghi#keys-1";
            var result = DidUrlParser.Parse(url);
            Assert.Equal(url, result.Url);
        }

        [Theory]
        [InlineData("did:web:example.com")]
        [InlineData("did:key:z6MkhaXgBZDvotDkL5257faiztiGiC2QtKLGpbnnEGta2doK")]
        [InlineData("did:pkh:eip155:1:0xb9c5714089478a327f09197987f16f9e5d936e8a")]
        [InlineData("did:ion:EiDyOQbbZAa3aiRzeCkV7LOx3SERjjH93EXoIM3UoN4oWg")]
        public void ShouldParseVariousDidMethods(string did)
        {
            var result = DidUrlParser.Parse(did);
            Assert.NotNull(result);
            Assert.NotNull(result.Method);
            Assert.NotNull(result.Id);
        }

        [Fact]
        public void DidUrlToStringShouldReturnUrl()
        {
            var result = DidUrlParser.Parse("did:example:123#frag");
            Assert.Equal("did:example:123#frag", result.ToString());
        }
    }
}
