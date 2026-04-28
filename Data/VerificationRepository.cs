using MySqlConnector;

namespace LuduvoBot.Data;

public sealed record PendingVerification(
    ulong DiscordUserId,
    string LuduvoUsername,
    string Token,
    DateTimeOffset ExpiresAt,
    DateTimeOffset RequestedAt
);

public sealed record VerifiedAccount(
    ulong DiscordUserId,
    uint LuduvoUserId,
    string LuduvoUsername,
    DateTimeOffset VerifiedAt
);

public sealed class VerificationRepository
{
    private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS user_verifications (
    discord_user_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
    luduvo_user_id INT UNSIGNED NULL,
    luduvo_username VARCHAR(64) NOT NULL,
    token VARCHAR(64) NULL,
    token_expires_at DATETIME NULL,
    verified_at DATETIME NULL,
    requested_at DATETIME NOT NULL,
    updated_at DATETIME NOT NULL,
    INDEX idx_luduvo_user_id (luduvo_user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

    private readonly string _connectionString;

    public VerificationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static string BuildConnectionStringFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("LUDUVO_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(raw))
            return raw;

        var host = Environment.GetEnvironmentVariable("LUDUVO_DB_HOST") ?? "192.168.1.17";
        var database = Environment.GetEnvironmentVariable("LUDUVO_DB_NAME") ?? "luduvo";
        var user = Environment.GetEnvironmentVariable("LUDUVO_DB_USER") ?? "luduvo";
        var password = Environment.GetEnvironmentVariable("LUDUVO_DB_PASSWORD")??"";

        var portRaw = Environment.GetEnvironmentVariable("LUDUVO_DB_PORT");
        var port = 3306;
        if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw, out var parsedPort))
            port = parsedPort;

        var builder = new MySqlConnectionStringBuilder
        {
            Server = host,
            Database = database,
            UserID = user,
            Password = password,
            Port = (uint)port,
            SslMode = MySqlSslMode.Preferred,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 10,
        };

        return builder.ConnectionString;
    }

    public async Task EnsureSchemaAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpsertPendingAsync(ulong discordUserId, string luduvoUsername, string token, DateTimeOffset expiresAt)
    {
        var now = DateTime.UtcNow;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO user_verifications
    (discord_user_id, luduvo_user_id, luduvo_username, token, token_expires_at, verified_at, requested_at, updated_at)
VALUES
    (@discord_user_id, NULL, @luduvo_username, @token, @token_expires_at, NULL, @requested_at, @updated_at)
ON DUPLICATE KEY UPDATE
    luduvo_user_id = NULL,
    luduvo_username = VALUES(luduvo_username),
    token = VALUES(token),
    token_expires_at = VALUES(token_expires_at),
    verified_at = NULL,
    requested_at = VALUES(requested_at),
    updated_at = VALUES(updated_at);";
        command.Parameters.AddWithValue("@discord_user_id", discordUserId);
        command.Parameters.AddWithValue("@luduvo_username", luduvoUsername);
        command.Parameters.AddWithValue("@token", token);
        command.Parameters.AddWithValue("@token_expires_at", expiresAt.UtcDateTime);
        command.Parameters.AddWithValue("@requested_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<PendingVerification?> GetPendingAsync(ulong discordUserId)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT discord_user_id, luduvo_username, token, token_expires_at, requested_at
FROM user_verifications
WHERE discord_user_id = @discord_user_id
  AND token IS NOT NULL
  AND token_expires_at IS NOT NULL
  AND verified_at IS NULL
LIMIT 1;";
        command.Parameters.AddWithValue("@discord_user_id", discordUserId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var token = reader.GetString("token");
        var expiresAt = ReadUtcDateTime(reader, "token_expires_at");
        var requestedAt = ReadUtcDateTime(reader, "requested_at");
        var luduvoUsername = reader.GetString("luduvo_username");
        return new PendingVerification(discordUserId, luduvoUsername, token, expiresAt, requestedAt);
    }

    public async Task ClearPendingAsync(ulong discordUserId)
    {
        var now = DateTime.UtcNow;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE user_verifications
SET token = NULL,
    token_expires_at = NULL,
    updated_at = @updated_at
WHERE discord_user_id = @discord_user_id;";
        command.Parameters.AddWithValue("@discord_user_id", discordUserId);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync();
    }

    public async Task MarkVerifiedAsync(ulong discordUserId, uint luduvoUserId, string luduvoUsername)
    {
        var now = DateTime.UtcNow;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE user_verifications
SET luduvo_user_id = @luduvo_user_id,
    luduvo_username = @luduvo_username,
    token = NULL,
    token_expires_at = NULL,
    verified_at = @verified_at,
    updated_at = @updated_at
WHERE discord_user_id = @discord_user_id;";
        command.Parameters.AddWithValue("@discord_user_id", discordUserId);
        command.Parameters.AddWithValue("@luduvo_user_id", luduvoUserId);
        command.Parameters.AddWithValue("@luduvo_username", luduvoUsername);
        command.Parameters.AddWithValue("@verified_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<VerifiedAccount?> GetVerifiedByDiscordIdAsync(ulong discordUserId)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT discord_user_id, luduvo_user_id, luduvo_username, verified_at
FROM user_verifications
WHERE discord_user_id = @discord_user_id
  AND luduvo_user_id IS NOT NULL
  AND verified_at IS NOT NULL
LIMIT 1;";
        command.Parameters.AddWithValue("@discord_user_id", discordUserId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var luduvoUserId = reader.GetUInt32("luduvo_user_id");
        var luduvoUsername = reader.GetString("luduvo_username");
        var verifiedAt = ReadUtcDateTime(reader, "verified_at");
        return new VerifiedAccount(discordUserId, luduvoUserId, luduvoUsername, verifiedAt);
    }

    public async Task<VerifiedAccount?> GetVerifiedByLuduvoUserIdAsync(uint luduvoUserId)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT discord_user_id, luduvo_user_id, luduvo_username, verified_at
FROM user_verifications
WHERE luduvo_user_id = @luduvo_user_id
  AND verified_at IS NOT NULL
LIMIT 1;";
        command.Parameters.AddWithValue("@luduvo_user_id", luduvoUserId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var discordUserId = reader.GetUInt64("discord_user_id");
        var luduvoUsername = reader.GetString("luduvo_username");
        var verifiedAt = ReadUtcDateTime(reader, "verified_at");
        return new VerifiedAccount(discordUserId, luduvoUserId, luduvoUsername, verifiedAt);
    }

    public async Task UnlinkAsync(ulong discordUserId)
    {
        var now = DateTime.UtcNow;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE user_verifications
SET luduvo_user_id = NULL,
    token = NULL,
    token_expires_at = NULL,
    verified_at = NULL,
    updated_at = @updated_at
WHERE discord_user_id = @discord_user_id;";
        command.Parameters.AddWithValue("@discord_user_id", discordUserId);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync();
    }

    private static DateTimeOffset ReadUtcDateTime(MySqlDataReader reader, string column)
    {
        var value = reader.GetDateTime(column);
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
