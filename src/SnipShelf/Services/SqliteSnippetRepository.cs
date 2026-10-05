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
    public Task<IReadOnlyList<Snippet>> SearchAsync(
        string? term = null,
        SortMode sort = SortMode.Updated,
        long? tagId = null,
        bool favoritesOnly = false,
        CancellationToken cancellationToken = default)
    {
        var filter = new SnippetFilter(tagId, favoritesOnly);

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
                var tagged = snippets.Select(s => s with { Tags = tags[s.Id] });

                if (string.IsNullOrWhiteSpace(term))
                {
                    return (IReadOnlyList<Snippet>)[.. tagged];
                }

                // SQLite LIKE folds ASCII only, so term matching happens here to keep CAFÉ/café
                // equivalent — the same OrdinalIgnoreCase matching used across the app.
                var needle = term.Trim();
                return (IReadOnlyList<Snippet>)[.. tagged.Where(s =>
                    s.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                    s.Body.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                    s.Tags.Any(t => t.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)))];
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

                var id = await UpsertInTransactionAsync(
                    connection, transaction, snippet, clock, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return id;
            },
            cancellationToken);
    }

    internal static async Task<long> UpsertInTransactionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Snippet snippet,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
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
        return id;
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
    /// case-insensitively in C# (OrdinalIgnoreCase), so "PowerShell" reuses "powershell" —
    /// tag identity is decided here, not by the database's ASCII-only NOCASE.
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
        var existingTags = await LoadTagsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        foreach (var name in wanted)
        {
            wantedIds.Add(await GetOrCreateTagAsync(connection, transaction, existingTags, name, cancellationToken).ConfigureAwait(false));
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
    /// Returns the id of the tag called <paramref name="name"/>, creating it when missing.
    /// <paramref name="existingTags"/> is the transaction's shared tag lookup: loading it
    /// happens in C# with OrdinalIgnoreCase because SQLite NOCASE folds ASCII only — under
    /// it, "CAFÉ" and "café" would become two rows. A lookup miss can never trip the NOCASE
    /// unique index on insert either: ASCII folding is a subset of OrdinalIgnoreCase
    /// folding, so NOCASE has no match whenever the lookup has none. New tags are added to
    /// the dictionary so the rest of the sync reuses them.
    /// </summary>
    private static async Task<long> GetOrCreateTagAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Dictionary<string, long> existingTags,
        string name,
        CancellationToken cancellationToken)
    {
        name = name.Trim();

        if (existingTags.TryGetValue(name, out var id))
        {
            return id;
        }

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Tags (Name) VALUES (@name);";
            insert.Parameters.AddWithValue("@name", name);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using var rowId = connection.CreateCommand();
        rowId.Transaction = transaction;
        rowId.CommandText = "SELECT last_insert_rowid();";
        id = Convert.ToInt64(await rowId.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        existingTags[name] = id;
        return id;
    }

    /// <summary>Snapshot of the Tags table, keyed for Unicode-insensitive lookup.</summary>
    private static async Task<Dictionary<string, long>> LoadTagsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var existing = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT Id, Name FROM Tags;";
        using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            existing[reader.GetString(1)] = reader.GetInt64(0);
        }

        return existing;
    }

    private static string Format(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    private static string WriteKind(SnippetKind kind) => kind.ToString().ToLowerInvariant();

    private static SnippetKind ReadKind(string value) =>
        Enum.TryParse(value, ignoreCase: true, out SnippetKind kind) ? kind : SnippetKind.Snippet;

    // The term is deliberately not a SQL filter: LIKE folds ASCII only, so SearchAsync
    // matches it in C# once the rows and their tags are in hand.
    private sealed class SnippetFilter(long? tagId, bool favoritesOnly)
    {
        public string Sql { get; } = BuildSql(tagId, favoritesOnly);

        public void Bind(SqliteCommand command)
        {
            if (tagId is not null)
            {
                command.Parameters.AddWithValue("@tagId", tagId.Value);
            }
        }

        private static string BuildSql(long? tagId, bool favoritesOnly)
        {
            var clauses = new List<string>();

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
    }
}
