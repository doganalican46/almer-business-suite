using System.Reflection;
using Npgsql;

namespace Almer.Infrastructure;

public static class SqlMigrationRunner
{
    public static async Task RunAsync(string connectionString, Assembly assembly, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using (var create = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS \"__AlmerMigrations\" (\"Name\" text PRIMARY KEY, \"AppliedAt\" timestamptz NOT NULL);", connection)) await create.ExecuteNonQueryAsync(ct);
        var resources = assembly.GetManifestResourceNames().Where(x => x.Contains("Database.Migrations", StringComparison.Ordinal) && x.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x).ToArray();
        foreach (var resource in resources)
        {
            var name = resource[(resource.LastIndexOf("Migrations.", StringComparison.Ordinal) + "Migrations.".Length)..];
            await using var exists = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM \"__AlmerMigrations\" WHERE \"Name\" = @name);", connection); exists.Parameters.AddWithValue("name", name);
            if ((bool)(await exists.ExecuteScalarAsync(ct) ?? false)) continue;
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException($"Migration resource {resource} was not found.");
            using var reader = new StreamReader(stream); var sql = await reader.ReadToEndAsync(ct);
            await using var command = new NpgsqlCommand(sql, connection, transaction); await command.ExecuteNonQueryAsync(ct);
            await using var record = new NpgsqlCommand("INSERT INTO \"__AlmerMigrations\" (\"Name\", \"AppliedAt\") VALUES (@name, now());", connection, transaction); record.Parameters.AddWithValue("name", name); await record.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
