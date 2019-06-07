using Nethereum.Rsk.Util;
using Xunit;

namespace Nethereum.Rsk.IntegrationTests
{
    public class UtilChecksumTest
    {
        [Theory]
        [InlineData("0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAeD", 30, true)]
        [InlineData("0xFb6916095cA1Df60bb79ce92cE3EA74c37c5d359", 30, true)]
        [InlineData("0xDBF03B407c01E7CD3cBea99509D93F8Dddc8C6FB", 30, true)]
        [InlineData("0xD1220A0Cf47c7B9BE7a2e6ba89F429762E7B9adB", 30, true)]
        [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", null, true)]
        [InlineData("0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359", null, true)]
        [InlineData("0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB", null, true)]
        [InlineData("0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb", null, true)]
        public void ValidateAddress(string address, int? network, bool isValid)
        {
            var result = AddressUtil.Current.IsChecksumAddress(address, network);
            Assert.Equal(isValid, result);
        }


        [Theory]
        [InlineData("0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAED", "0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAeD", 30)]
        [InlineData("0xFb6916095cA1Df60bb79ce92cE3EA74c37C5d359","0xFb6916095cA1Df60bb79ce92cE3EA74c37c5d359", 30)]
        [InlineData("0xDBF03B407c01E7CD3cBea99509D93F8DddC8C6FB","0xDBF03B407c01E7CD3cBea99509D93F8Dddc8C6FB", 30 )]
        [InlineData("0xD1220A0Cf47c7B9BE7a2e6ba89F429762E7B9adB","0xD1220A0Cf47c7B9BE7a2e6ba89F429762E7B9adB", 30 )]
        [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed","0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", null )]
        [InlineData("0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359","0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359", null )]
        [InlineData("0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB","0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB", null )]
        [InlineData("0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb","0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb", null)]
        public void ValidateAddressConversion(string addressOriginal, string addressExpected, int? network)
        {
            var converted = AddressUtil.Current.ConvertToChecksumAddress(addressOriginal, network);
            Assert.Equal(addressExpected,converted);
        }
    }
}