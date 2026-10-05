using System.Globalization;
using Microsoft.Data.Sqlite;
using SnipShelf.Models;

namespace SnipShelf.Services;

/// <summary>SQLite-backed <see cref="ISnippetRepository"/>.</summary>
/// <remarks>
/// Every member hops to the thread pool: Microsoft.Data.Sqlite is synchronous, and the PRD
/// budgets expect the UI thread never to wait on the database. One connection per call —
/// connection pooling makes that cheap and keeps callers from sharing a connection they
/// would have to serialize themselves.
/// </remarks>
public sealed class SqliteSnippetRepository(VaultDatabase database, TimeProvider clock) : ISnippetRepository
{
    private const string EscapeCharacter = "\\";

    public Task<IReadOnlyList<Snippet>> SearchAsync(
        string? term = null,
        SortMode sort = SortMode.Updated,
        long? tagId = null,
        bool favoritesOnly = false,
        CancellationToken cancellationToken = default)
    {
        var filter = new SnippetFilter(term, tagId, favoritesOnly);

        return Task.Run(
            async () =>
            {
                using var connection = database.OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT s.Id, s.Title, s.Body, s.Kind, s.Favorite, s.CreatedUtc, s.UpdatedUtc
                    FROM Snippets s
                    WHERE {filter.Sql}
                    ORDER BY {OrderBy(sort)};
                    """;
                filter.Bind(command);

                var snippets = new List<Snippet>();
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        snippets.Add(ReadSnippet(reader));
                    }
                }

                    var tags = await ReadTagsAsync(connection, snippets, cancellationToken).ConfigureAwait(false);
                    return (IReadOnlyList<Snippet>)[.. snippets.Select(s => s with { Tags = tags[s.Id] })];
            },
            cancellationToken);
    }

    public Task<long> UpsertAsync(Snippet snippet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        ArgumentException.ThrowIfNullOrWhiteSpace(snippet.Title);
        ArgumentNullException.ThrowIfNull(snippet.Body);

        return Task.Run(
            async () =>
            {
                using var connection = database.OpenConnection();
                using var transaction = connection.BeginTransaction();

                long id;
                if (snippet.Id == 0)
                {
                    var now = Format(clock.GetUtcNow());
                    using var insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO Snippets (Title, Body, Kind, Favorite, CreatedUtc, UpdatedUtc)
                        VALUES (@title, @body, @kind, @favorite, @created, @created);
                        SELECT last_insert_rowid();
                        """;
                    insert.Parameters.AddWithValue("@title", snippet.Title.Trim());
                    insert.Parameters.AddWithValue("@body", snippet.Body);
                    insert.Parameters.AddWithValue("@kind", WriteKind(snippet.Kind));
                    insert.Parameters.AddWithValue("@favorite", snippet.Favorite ? 1 : 0);
                    insert.Parameters.AddWithValue("@created", now);
                    id = Convert.ToInt64(await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
                }
                else
                {
                    using var update = connection.CreateCommand();
                    update.Transaction = transaction;
                    update.CommandText = """
                        UPDATE Snippets
                        SET Title = @title, Body = @body, Kind = @kind, Favorite = @favorite, UpdatedUtc = @updated
                        WHERE Id = @id;
                        """;
                    update.Parameters.AddWithValue("@title", snippet.Title.Trim());
                    update.Parameters.AddWithValue("@body", snippet.Body);
                    update.Parameters.AddWithValue("@kind", WriteKind(snippet.Kind));
                    update.Parameters.AddWithValue("@favorite", snippet.Favorite ? 1 : 0);
                    update.Parameters.AddWithValue("@updated", Format(clock.GetUtcNow()));
                    update.Parameters.AddWithValue("@id", snippet.Id);
                    var rowsAffected = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    if (rowsAffected == 0)
                    {
                        throw new InvalidOperationException($"Snippet with Id {snippet.Id} not found or was deleted.");
                    }
                    id = snippet.Id;
                }

                await SyncTagsAsync(connection, transaction, id, snippet.Tags, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return id;
            },
            cancellationToken);
    }

    public Task<Snippet?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            async () =>
            {
                using var connection = database.OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT s.Id, s.Title, s.Body, s.Kind, s.Favorite, s.CreatedUtc, s.UpdatedUtc
                    FROM Snippets s
                    WHERE s.Id = @id;
                    """;
                command.Parameters.AddWithValue("@id", id);

                Snippet? snippet = null;
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        snippet = ReadSnippet(reader);
                    }
                }

                if (snippet is not null)
                {
                    var tags = await ReadTagsAsync(connection, [snippet], cancellationToken).ConfigureAwait(false);
                    snippet = snippet with { Tags = tags[snippet.Id] };
                }

                return snippet;
            },
            cancellationToken);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            async () =>
            {
                using var connection = database.OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM Snippets WHERE Id = @id;";
                command.Parameters.AddWithValue("@id", id);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<TagCount>> GetTagsWithCountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            async () =>
            {
                using var connection = database.OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT t.Id, t.Name, COUNT(st.SnippetId) AS SnippetCount
                    FROM Tags t
                    LEFT JOIN SnippetTags st ON st.TagId = t.Id
                    GROUP BY t.Id, t.Name
                    ORDER BY SnippetCount DESC, t.Name COLLATE NOCASE ASC;
                    """;

                var results = new List<TagCount>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    results.Add(new TagCount(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetInt32(2)));
                }

                return (IReadOnlyList<TagCount>)results;
            },
            cancellationToken);
    }

    private static string OrderBy(SortMode sort) => sort switch
    {
        SortMode.Title => "s.Title COLLATE NOCASE ASC, s.Id ASC",
        SortMode.Created => "s.CreatedUtc DESC, s.Id DESC",
        _ => "s.UpdatedUtc DESC, s.Id DESC",
    };

    private static Snippet ReadSnippet(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Title = reader.GetString(1),
        Body = reader.GetString(2),
        Kind = ReadKind(reader.GetString(3)),
        Favorite = reader.GetInt64(4) != 0,
        CreatedUtc = Parse(reader.GetString(5)),
        UpdatedUtc = Parse(reader.GetString(6)),
    };

    private static async Task<Dictionary<long, IReadOnlyList<Tag>>> ReadTagsAsync(
        SqliteConnection connection,
        List<Snippet> snippets,
        CancellationToken cancellationToken)
    {
        var bySnippet = snippets.ToDictionary(s => s.Id, _ => (IReadOnlyList<Tag>)new List<Tag>());
        if (snippets.Count == 0)
        {
            return bySnippet;
        }

        using var command = connection.CreateCommand();
        var ids = string.Join(',', snippets.Select(s => s.Id));
        command.CommandText = $"""
            SELECT st.SnippetId, t.Id, t.Name
            FROM SnippetTags st
            JOIN Tags t ON t.Id = st.TagId
            WHERE st.SnippetId IN ({ids})
            ORDER BY t.Name COLLATE NOCASE;
            """;

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ((List<Tag>)bySnippet[reader.GetInt64(0)]).Add(new Tag(reader.GetInt64(1), reader.GetString(2)));
        }

        return bySnippet;
    }

    /// <summary>
    /// Brings the snippet's tag links in line with <paramref name="tags"/>. Names are compared
    /// case-insensitively (Tags.Name is COLLATE NOCASE), so "PowerShell" reuses "powershell"
    /// rather than tripping the unique index.
    /// </summary>
    private static async Task SyncTagsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long snippetId,
        IReadOnlyList<Tag> tags,
        CancellationToken cancellationToken)
    {
        var wanted = tags
            .Select(tag => tag.Name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var wantedIds = new List<long>();
        foreach (var name in wanted)
        {
            wantedIds.Add(await GetOrCreateTagAsync(connection, transaction, name, cancellationToken).ConfigureAwait(false));
        }

        using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = wantedIds.Count == 0
                ? "DELETE FROM SnippetTags WHERE SnippetId = @snippetId;"
                : $"DELETE FROM SnippetTags WHERE SnippetId = @snippetId AND TagId NOT IN ({string.Join(',', wantedIds)});";
            clear.Parameters.AddWithValue("@snippetId", snippetId);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var tagId in wantedIds)
        {
            await LinkTagAsync(connection, transaction, snippetId, tagId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task LinkTagAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long snippetId,
        long tagId,
        CancellationToken cancellationToken)
    {
        using var link = connection.CreateCommand();
        link.Transaction = transaction;
        link.CommandText = "INSERT OR IGNORE INTO SnippetTags (SnippetId, TagId) VALUES (@snippetId, @tagId);";
        link.Parameters.AddWithValue("@snippetId", snippetId);
        link.Parameters.AddWithValue("@tagId", tagId);
        await link.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts the tag unless it is already there, then reads its id back. Two statements
    /// rather than one because a multi-statement command only reports the first one's result.
    /// </summary>
    private static async Task<long> GetOrCreateTagAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string name,
        CancellationToken cancellationToken)
    {
        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Tags (Name) VALUES (@name) ON CONFLICT (Name) DO NOTHING;";
            insert.Parameters.AddWithValue("@name", name);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT Id FROM Tags WHERE Name = @name;";
        select.Parameters.AddWithValue("@name", name);
        return Convert.ToInt64(await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static string Format(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    private static string WriteKind(SnippetKind kind) => kind.ToString().ToLowerInvariant();

    private static SnippetKind ReadKind(string value) =>
        Enum.TryParse(value, ignoreCase: true, out SnippetKind kind) ? kind : SnippetKind.Snippet;

    private sealed class SnippetFilter(string? term, long? tagId, bool favoritesOnly)
    {
        public string Sql { get; } = BuildSql(term, tagId, favoritesOnly);

        public void Bind(SqliteCommand command)
        {
            if (!string.IsNullOrWhiteSpace(term))
            {
                command.Parameters.AddWithValue("@term", $"%{Escape(term.Trim())}%");
            }

            if (tagId is not null)
            {
                command.Parameters.AddWithValue("@tagId", tagId.Value);
            }
        }

        private static string BuildSql(string? term, long? tagId, bool favoritesOnly)
        {
            var clauses = new List<string>();

            if (!string.IsNullOrWhiteSpace(term))
            {
                clauses.Add($"""
                    (s.Title LIKE @term ESCAPE '{EscapeCharacter}'
                     OR s.Body LIKE @term ESCAPE '{EscapeCharacter}'
                     OR EXISTS (SELECT 1 FROM SnippetTags st JOIN Tags t ON t.Id = st.TagId
                                WHERE st.SnippetId = s.Id AND t.Name LIKE @term ESCAPE '{EscapeCharacter}'))
                    """);
            }

            if (tagId is not null)
            {
                clauses.Add("EXISTS (SELECT 1 FROM SnippetTags st WHERE st.SnippetId = s.Id AND st.TagId = @tagId)");
            }

            if (favoritesOnly)
            {
                clauses.Add("s.Favorite = 1");
            }

            return clauses.Count == 0 ? "1" : string.Join(" AND ", clauses);
        }

        /// <summary>
        /// Makes LIKE treat the user's % and _ as literal characters. Without this, searching
        /// for "100%" matches everything.
        /// </summary>
        private static string Escape(string term) => term
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
    }
}
