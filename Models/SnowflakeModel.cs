using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Snowflake.Data.Client;

namespace data_sentry.Models
{
    public partial class SnowflakeModel : ObservableObject, IDisposable
    {
        // Add a private connection field to maintain the connection
        private SnowflakeDbConnection _connection;
        private bool _isDisposed;

        // Connection properties
        [ObservableProperty]
        private string? account;

        [ObservableProperty]
        private string? user;

        [ObservableProperty]
        private string? password;

        [ObservableProperty]
        private string? database;

        [ObservableProperty]
        private string? schema;

        [ObservableProperty]
        private string? warehouse;

        [ObservableProperty]
        private string? role;

        // 3rd Party Authentication
        [ObservableProperty]
        private string? authenticator;

        [ObservableProperty]
        private string? token;

        // Status
        [ObservableProperty]
        private string? connectionStatus;

        // Track connection state
        [ObservableProperty]
        private bool isConnected;

        private string GetConnectionString()
        {
            var connectionString = $"account={Account};user={User};";

            // Add authenticator if specified
            if (!string.IsNullOrEmpty(Authenticator))
            {
                connectionString += $"authenticator={Authenticator};";

                // For OAuth or other token-based auth
                if (!string.IsNullOrEmpty(Token))
                {
                    connectionString += $"token={Token};";
                }
            }
            else if (!string.IsNullOrEmpty(Password))
            {
                // Only add password for standard auth
                connectionString += $"password={Password};";
            }

            // Add remaining connection parameters
            connectionString += $"db={Database};schema={Schema};warehouse={Warehouse};role={Role};";
            return connectionString;
        }

        // Ensure the connection is open and ready to use
        private async Task<SnowflakeDbConnection> EnsureConnectionAsync()
        {
            try
            {
                if (_connection == null)
                {
                    // Create new connection if none exists
                    _connection = new SnowflakeDbConnection();
                    _connection.ConnectionString = GetConnectionString();
                }

                // Only open if not already open
                if (_connection.State != ConnectionState.Open)
                {
                    await _connection.OpenAsync();
                    IsConnected = true;
                    ConnectionStatus = "Connected successfully";
                }

                return _connection;
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Connection failed: {ex.Message}";
                IsConnected = false;

                // Clean up failed connection attempt
                if (_connection != null)
                {
                    _connection.Dispose();
                    _connection = null;
                }

                throw; // Rethrow to handle in calling methods
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // Use the shared connection
                await EnsureConnectionAsync();
                return true;
            }
            catch (Exception)
            {
                return false; // Error message already set in EnsureConnectionAsync
            }
        }

        public async Task<System.Text.Json.JsonElement> ExecuteQueryAsync(string sqlQuery)
        {
            try
            {
                // Get or establish the connection
                var conn = await EnsureConnectionAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sqlQuery;

                using var reader = await cmd.ExecuteReaderAsync();
                var dataTable = new DataTable();
                dataTable.Load(reader);
                Console.WriteLine($"Rows returned: {dataTable.Rows.Count}");
                var rows = new List<Dictionary<string, object>>();
                foreach (DataRow row in dataTable.Rows)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (DataColumn col in dataTable.Columns)
                    {
                        dict[col.ColumnName] = row[col];
                    }
                    rows.Add(dict);
                }

                var jsonString = System.Text.Json.JsonSerializer.Serialize(rows);
                var jsonDocument = System.Text.Json.JsonDocument.Parse(jsonString);
                var jsonElement = jsonDocument.RootElement.Clone();

                ConnectionStatus = $"Query executed successfully. Rows returned: {dataTable.Rows.Count}";
                return jsonElement;
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Query failed: {ex.Message}";
                Console.WriteLine(ex.ToString());

                // Create a JSON structure with error details instead of returning an empty array
                var errorResult = new Dictionary<string, object>
                {
                    ["error"] = true,
                    ["message"] = ex.Message,
                    ["details"] = ex.ToString(),
                    ["errorType"] = ex.GetType().Name
                };

                string jsonString = JsonSerializer.Serialize(errorResult, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return JsonDocument.Parse(jsonString).RootElement;
            }
        }
        // Explicit method to close connection
        public void CloseConnection()
        {
            if (_connection != null && _connection.State == ConnectionState.Open)
            {
                _connection.Close();
                IsConnected = false;
                ConnectionStatus = "Connection closed";
            }
        }
        // Implement IDisposable to properly clean up
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Clean up managed resources
                    if (_connection != null)
                    {
                        CloseConnection();
                        _connection.Dispose();
                        _connection = null;
                    }
                }

                _isDisposed = true;
            }
        }
        // Finalizer
        ~SnowflakeModel()
        {
            Dispose(false);
        }
    }
}