using Nethereum.DataServices.FourByteDirectory;
using System.Linq;
using Xunit;

namespace Nethereum.DataServices.IntegrationTests
{
    public class FourByteDirectoryServiceTests
    {
        [Fact]
        public async void ShouldGetFunctionSignatureByHexSignature()
        {
            var fourByteDirectoryService = new FourByteDirectoryService();
            var signature = await fourByteDirectoryService.GetFunctionSignatureByHexSignatureAsync("0x722713f7");
            Assert.NotNull(signature);
            Assert.Equal(1, signature.Count);
            Assert.Single(signature.Signatures);
            Assert.Equal("balanceOf()", signature.Signatures[0].TextSignature);
        }

        [Fact]
        public async void ShouldGetEventSignatureByHexSignature()
        {
            var fourByteDirectoryService = new FourByteDirectoryService();
            var signature = await fourByteDirectoryService.GetEventSignatureByHexSignatureAsync("0x15aac4af776447c09d895192c86bab463c38b92191f3ba3f7b8831723c548d6e");
            Assert.NotNull(signature);
            Assert.Equal(1, signature.Count);
            Assert.Single(signature.Signatures);
            Assert.Equal("RequestCreated(address,address,int256,uint256[12])", signature.Signatures[0].TextSignature);
        }


        [Fact]
        public async void ShouldGetEventSignatureByTextSignatureInsensitive()
        {
            var fourByteDirectoryService = new FourByteDirectoryService();
            var signature = await fourByteDirectoryService.GetEventSignatureByTextSignatureInsensitiveAsync("RequestCreated(address,address,int256,uint256[12])");
            Assert.NotNull(signature);
        }

        [Fact]
        public async void ShouldGetEventSignatureByTextSignature()
        {
            var fourByteDirectoryService = new FourByteDirectoryService();
            var signature = await fourByteDirectoryService.GetEventSignatureByTextSignatureAsync("RequestCreated(address,address,int256,uint256[12])");
            Assert.NotNull(signature);
            Assert.Contains(signature.Signatures, x => x.TextSignature == "RequestCreated(address,address,int256,uint256[12])");

        }


        [Fact]
        public async void ShouldGetFunctionSignatureByTextSignature()
        {
            var fourByteDirectoryService = new FourByteDirectoryService();
            var signature = await fourByteDirectoryService.GetFunctionSignatureByTextSignatureAsync("Packet(bytes)");
            Assert.NotNull(signature);
            Assert.Contains(signature.Signatures, x => x.TextSignature == "Packet(bytes)");
        }

        [Fact]
        public async void ShouldGetFunctionSignatureByTextSignatureInsensitive()
        {
            var fourByteDirectoryService = new FourByteDirectoryService();
            var signature = await fourByteDirectoryService.GetFunctionSignatureByTextSignatureInsensitiveAsync("Packet(bytes)");
            Assert.NotNull(signature);
        }
    }
}

