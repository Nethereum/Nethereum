using System;
using Xunit;

namespace Nethereum.Util.Tests
{
    public class AddressUtilTests
    {

        [Fact]
        public virtual void ShouldCreateACheckSumAddress()
        {
            var address1 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            var address2 = "0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359";
            var address3 = "0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB";
            var address4 = "0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb";
            Assert.Equal(address1, ToChecksumAddress(address1.ToUpper()));
            Assert.Equal(address2, ToChecksumAddress(address2.ToUpper()));
            Assert.Equal(address3, ToChecksumAddress(address3.ToUpper()));
            Assert.Equal(address4, ToChecksumAddress(address4.ToUpper()));
        }

        [Fact]
        public virtual void ShouldCheckIsCheckSumAddress()
        {
            var address1 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            var address1F = "0x5aaeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
            var address2 = "0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359";
            var address2F = "0xfb6916095ca1df60bB79Ce92cE3Ea74c37c5d359";
            var address3 = "0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB";
            var address4 = "0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb";
            var addressUtil = new AddressUtil();
            Assert.True(addressUtil.IsChecksumAddress(address1));
            Assert.False(addressUtil.IsChecksumAddress(address1F));
            Assert.True(addressUtil.IsChecksumAddress(address2));
            Assert.False(addressUtil.IsChecksumAddress(address2F));
            Assert.True(addressUtil.IsChecksumAddress(address3));
            Assert.True(addressUtil.IsChecksumAddress(address4));

        }

        public string ToChecksumAddress(string address)
        {
            return new AddressUtil().ConvertToChecksumAddress(address);
        }
    }
}
