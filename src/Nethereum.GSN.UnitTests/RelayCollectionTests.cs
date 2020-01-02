using Moq;
using Nethereum.GSN.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Nethereum.GSN.UnitTests
{
    public class RelayCollectionTests
    {
        private readonly Mock<IRelayClient> _client;

        public RelayCollectionTests()
        {
            _client = new Mock<IRelayClient>();
        }

        [Fact]
        public void RelayCollection_EmptyCollection()
        {
            // Act
            var collection = new RelayCollection(_client.Object, new List<RelayOnChain>());

            // Assert
            Assert.Empty(collection);
        }

        [Fact]
        public void RelayCollection_SingleItem()
        {
            // Arrange
            var list = new List<RelayOnChain>()
            {
                new RelayOnChain
                {
                    Url = "http://test.url"
                }
            };
            _client.Setup(x => x.GetAddrAsync(It.IsAny<Uri>()))
                .ReturnsAsync(new GetAddrResponse() { Ready = true });

            // Act
            var collection = new RelayCollection(_client.Object, list);

            // Assert
            Assert.Single(collection);
            var relay = collection.First().Value;
            Assert.True(relay.IsLoaded);
            Assert.True(relay.Ready);
        }

        [Fact]
        public void RelayCollection_SingleItemWithInvalidUrl()
        {
            // Arrange
            var list = new List<RelayOnChain>()
            {
                new RelayOnChain()
            };

            // Act
            var collection = new RelayCollection(_client.Object, list);

            // Assert
            Assert.Single(collection);
            var relay = collection.First().Value;
            Assert.False(relay.IsLoaded);
            Assert.False(relay.Ready);
        }
    }
}
