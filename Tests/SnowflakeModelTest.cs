using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Data;
using System.Linq;
using data_sentry.Models;
using System;

namespace data_sentry.Tests
{
    [TestClass]
    public class SnowflakeModelTests
    {
        // Use Ignore attribute to skip tests requiring credentials
        [TestMethod]
        public async Task TestSnowflakeQuery()
        {
            // Arrange
            var model = new SnowflakeModel
            {
                Account = "",
                User = "",
                Database = "",
                Schema = "PUBLIC",
                Warehouse = "",
                Role = "",
                Authenticator = "externalbrowser"
            };

            // Test connection first
            bool connected = await model.TestConnectionAsync();
            Assert.IsTrue(connected, $"Connection failed: {model.ConnectionStatus}");

            // Act - Simple query that should work on any Snowflake instance
            var result = await model.ExecuteQueryAsync("SELECT current_user() AS USER, current_role() AS ROLE");

            // Assert
            Assert.IsNotNull(result);
        }

    }
}