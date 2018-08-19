using System;
using System.Net;
using CollaborationDemo.WCF.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CollaborationDemo.Tests
{
    [TestClass]
    public class NetworkAddressTests
    {
        [TestMethod]
        public void ShouldParseStringRepresentation()
        {
            //Arrange
            var stringRepresentation = "192.168.1.0/24";
            var expectedAddress = IPAddress.Parse("192.168.1.0");
            var expectedSignificantBits = 24;

            //Act
            var networdAddress = NetworkAddress.Parse(stringRepresentation);

            //Assert
            Assert.AreEqual(expectedAddress, networdAddress.Address);
            Assert.AreEqual(expectedSignificantBits, networdAddress.SignificantBits);
        }

        [TestMethod]
        public void ShouldSanitizeNetworkAddress()
        {
            //Arrange
            var stringRepresentation = "192.168.191.128/19";
            var expectedAddress = IPAddress.Parse("192.168.160.0");

            //Act
            var actualAddress = NetworkAddress.Parse(stringRepresentation).Address;

            //Assert
            Assert.AreEqual(expectedAddress, actualAddress);
        }

        [TestMethod]
        public void ShouldCalculateBroadcastAddress()
        {
            //Arrange
            var stringRepresentation = "192.168.160.0/19";
            var expectedAddress = IPAddress.Parse("192.168.191.255");

            //Act
            var actualAddress = NetworkAddress.Parse(stringRepresentation).BroadcastAddress;

            //Assert
            Assert.AreEqual(expectedAddress, actualAddress);
        }
    }
}
