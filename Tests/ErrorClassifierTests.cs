using System;
using L2TitanLauncher.Services;
using Xunit;

namespace L2TitanLauncher.Tests
{
    public class ErrorClassifierTests
    {
        [Theory]
        [InlineData("Could not connect to server. Please try again.")]
        [InlineData("A connection attempt failed because the connected party did not respond")]
        public void Classify_Connection(string message) =>
            Assert.Equal(UpdateErrorKind.Connection, ErrorClassifier.Classify(new Exception(message)));

        [Theory]
        [InlineData("Access to the path 'C:\\X' is denied.")]
        [InlineData("access is denied")]
        [InlineData("No write permission in game folder")]
        public void Classify_Permission(string message) =>
            Assert.Equal(UpdateErrorKind.Permission, ErrorClassifier.Classify(new Exception(message)));

        [Fact]
        public void Classify_Generic_WhenNoKnownSubstring() =>
            Assert.Equal(UpdateErrorKind.Generic, ErrorClassifier.Classify(new Exception("something unexpected happened")));
    }
}
