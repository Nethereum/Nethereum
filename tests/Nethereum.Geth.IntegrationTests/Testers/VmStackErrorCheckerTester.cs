using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.IntegrationTests.Testers
{
    public class VmStackErrorCheckerTester
    {
        private const string StackWithoutError = @"
{
  ""gas"": 50440,
  ""failed"": false,
  ""returnValue"": ""0000000000000000000000000000000000000000000000000000000000000001"",
  ""structLogs"": [
    {
      ""pc"": 0,
      ""op"": ""PUSH1"",
      ""gas"": 29168,
      ""gasCost"": 3,
      ""depth"": 1,
      ""stack"": []
    }]
}
";

        private const string StackWithError = @"
{
  ""structLogs"": [
    {
      ""error"": ""oops""
    }]
}
";

        [Fact]
        public void ShouldReturnNullWhenStackDoesNotContainAnError()
        {
            var error = new VmStackErrorChecker().GetError(JObject.Parse(StackWithoutError));
            Assert.Null(error);
        }

        [Fact]
        public void ShouldReturnErrorFromStackTrace()
        {
            var error = new VmStackErrorChecker().GetError(JObject.Parse(StackWithError));
            Assert.Equal("oops", error);
        }
    }
}
