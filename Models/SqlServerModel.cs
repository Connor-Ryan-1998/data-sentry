using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace data_sentry.Models
{
    public partial class SqlServerModel : ObservableObject, IDisposable
    {
        // Add a private connection field to maintain the connection
        private SqlConnection _connection;
        private bool _isDisposed;

        // Connection properties
        [ObservableProperty]
        private string? server;

        [ObservableProperty]
        private string? database;

        [ObservableProperty]
        private string? user;

        [ObservableProperty]
        private string? password;

        // Authentication options
        [ObservableProperty]
        private bool useIntegratedSecurity;

        [ObservableProperty]
        private int connectionTimeout = 30;

        [ObservableProperty]
        private bool trustServerCertificate = true;

        // Status
        [ObservableProperty]
        private string? connectionStatus;

        // Track connection state
        [ObservableProperty]
        private bool isConnected;

        private string GetConnectionString()
        {
            var connectionString = $"Server={Server};Database={Database};";

            if (UseIntegratedSecurity)
            {
                connectionString += "Integrated Security=True;";
            }
            else
            {
                connectionString += $"User Id={User};Password={Password};";
            }

            connectionString += $"Connection Timeout={ConnectionTimeout};";
            connectionString += $"TrustServerCertificate={TrustServerCertificate};";
            connectionString += "Encrypt=False;"; // Disable encryption to avoid TLS issues
            Console.WriteLine($"Connection String: {connectionString}");
            return connectionString;
        }

        // Ensure the connection is open and ready to use
        private async Task<SqlConnection> EnsureConnectionAsync()
        {
            try
            {
                if (_connection == null)
                {
                    // Create new connection if none exists
                    _connection = new SqlConnection(GetConnectionString());
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

        public async Task<JsonElement> ExecuteQueryAsync(string sqlQuery)
        {
            try
            {
                // Get or establish the connection
                var conn = await EnsureConnectionAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sqlQuery;
                cmd.CommandTimeout = ConnectionTimeout;

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
                        // Handle DBNull values appropriately
                        dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                    }
                    rows.Add(dict);
                }

                var jsonString = JsonSerializer.Serialize(rows, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var jsonDocument = JsonDocument.Parse(jsonString);
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

        // Generic query execution for mapping to typed results
        public async Task<List<T>> ExecuteQueryAsync<T>(string sqlQuery, Func<SqlDataReader, T> mapper)
        {
            try
            {
                var conn = await EnsureConnectionAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sqlQuery;
                cmd.CommandTimeout = ConnectionTimeout;

                using var reader = await cmd.ExecuteReaderAsync();
                var results = new List<T>();

                while (await reader.ReadAsync())
                {
                    results.Add(mapper(reader));
                }

                ConnectionStatus = $"Query executed successfully. Rows returned: {results.Count}";
                return results;
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Query failed: {ex.Message}";
                Console.WriteLine(ex.ToString());
                return new List<T>();
            }
        }

        // Execute non-query (INSERT, UPDATE, DELETE) with parameter support
        public async Task<int> ExecuteNonQueryAsync(string sqlCommand, Dictionary<string, object> parameters = null)
        {
            try
            {
                var conn = await EnsureConnectionAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sqlCommand;
                cmd.CommandTimeout = ConnectionTimeout;

                // Add parameters if provided
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        var sqlParam = cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                ConnectionStatus = $"Command executed successfully. Rows affected: {rowsAffected}";
                return rowsAffected;
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Command failed: {ex.Message}";
                Console.WriteLine(ex.ToString());
                return -1;
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
        ~SqlServerModel()
        {
            Dispose(false);
        }
    }
}