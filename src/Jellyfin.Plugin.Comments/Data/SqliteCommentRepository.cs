using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Comments.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.Comments.Data;

/// <summary>
/// SQLite implementation of the comment repository.
/// </summary>
public class SqliteCommentRepository : ICommentRepository
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteCommentRepository"/> class.
    /// Wir lassen uns per Dependency Injection die Pfade von Jellyfin geben (IApplicationPaths).
    /// </summary>
    public SqliteCommentRepository(IApplicationPaths applicationPaths)
    {
        // Wir legen unsere Datenbankdatei (comments.db) direkt im sicheren Data-Ordner von Jellyfin ab.
        var dbPath = Path.Combine(applicationPaths.DataPath, "comments.db");
        
        // Der Connection-String sagt dem SQLite-Treiber, wo die Datei liegt.
        _connectionString = $"Data Source={dbPath}";
    }

    // Hilfsmethode, um nicht jedes Mal "new SqliteConnection" tippen zu müssen.
    private SqliteConnection GetConnection() => new(_connectionString);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 'await using' stellt sicher, dass die Verbindung am Ende der Methode sauber geschlossen wird (Memory Leak Prävention).
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        // Wir nutzen C# 11+ "Raw String Literals" ("""). Das macht SQL-Code extrem lesbar, ohne störende + oder \n.
        // WICHTIG: SQLite kennt keine Datentypen wie 'Guid' oder 'DateTime'. Alles wird als 'TEXT' gespeichert.
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS Comments (
                Id TEXT PRIMARY KEY,
                ItemId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                ParentCommentId TEXT,
                Text TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            );
            """;

        await using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddCommentAsync(StoredComment comment, CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        // Wir nutzen @-Parameter (@Id, @Text etc.). Das schützt uns vor SQL-Injection!
        // Niemals Strings einfach mit einem + zusammenbauen.
        const string sql = """
            INSERT INTO Comments (Id, ItemId, UserId, ParentCommentId, Text, CreatedAt, UpdatedAt)
            VALUES (@Id, @ItemId, @UserId, @ParentCommentId, @Text, @CreatedAt, @UpdatedAt);
            """;

        await using var command = new SqliteCommand(sql, connection);
        
        // Wir wandeln unsere C#-Typen in Strings um, die SQLite versteht ("O" = ISO 8601 Format für Datum).
        command.Parameters.AddWithValue("@Id", comment.Id.ToString());
        command.Parameters.AddWithValue("@ItemId", comment.ItemId.ToString());
        command.Parameters.AddWithValue("@UserId", comment.UserId.ToString());
        command.Parameters.AddWithValue("@ParentCommentId", comment.ParentCommentId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Text", comment.Text);
        command.Parameters.AddWithValue("@CreatedAt", comment.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", comment.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<StoredComment?> GetCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM Comments WHERE Id = @Id LIMIT 1;";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", commentId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            return MapReaderToComment(reader);
        }

        return null; // Kommentar nicht gefunden
    }

    public async Task<IReadOnlyList<StoredComment>> GetCommentsForItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        // Wir sortieren direkt aufsteigend nach Erstelldatum
        const string sql = "SELECT * FROM Comments WHERE ItemId = @ItemId ORDER BY CreatedAt ASC;";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        var results = new List<StoredComment>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapReaderToComment(reader));
        }

        return results;
    }

    public async Task UpdateCommentAsync(StoredComment comment, CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE Comments 
            SET Text = @Text, UpdatedAt = @UpdatedAt 
            WHERE Id = @Id;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", comment.Id.ToString());
        command.Parameters.AddWithValue("@Text", comment.Text);
        command.Parameters.AddWithValue("@UpdatedAt", comment.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        // Wir löschen den Kommentar. 
        // (Für die Zukunft: Wir löschen auch alle Replies, die diesen Kommentar als Parent haben)
        const string sql = "DELETE FROM Comments WHERE Id = @Id OR ParentCommentId = @Id;";
        
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", commentId.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Hilfsmethode, um eine Zeile aus der Datenbank (SqliteDataReader) wieder in unser C#-Objekt zu übersetzen.
    /// </summary>
    private static StoredComment MapReaderToComment(SqliteDataReader reader)
    {
        // Wir lesen die Spalten über ihren Index (0 = Id, 1 = ItemId, usw.) anhand unserer SELECT / CREATE Reihenfolge.
        return new StoredComment
        {
            Id = Guid.Parse(reader.GetString(0)),
            ItemId = Guid.Parse(reader.GetString(1)),
            UserId = Guid.Parse(reader.GetString(2)),
            ParentCommentId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
            Text = reader.GetString(4),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(5)),
            UpdatedAt = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6))
        };
    }
}