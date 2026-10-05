using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SnipShelf.Services;

/// <summary>
/// The vault on disk: <c>snipvault.db</c> in the app data folder, plus how to reach it.
/// </summary>
/// <remarks>
/// Owning the connection factory here is the point. SQLite has foreign keys off by default,
/// so a connection opened anywhere else would quietly break the ON DELETE CASCADE between
/// Snippets and their tags.
/// </remarks>
public sealed class VaultDatabase
{
    public const string FileName = "snipvault.db";

    public VaultDatabase(string dataFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
        DataFolder = dataFolder;
        DatabasePath = Path.Combine(dataFolder, FileName);
    }

    public string DataFolder { get; }

    public string DatabasePath { get; }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            ForeignKeys = true,
        }.ToString());

        // SQLite LIKE folds ASCII only, so the like() function is overridden (both arities:
        // X LIKE Y ESCAPE Z uses the three-argument form) with the app-wide OrdinalIgnoreCase
        // folding — CAFÉ finds café in SQL instead of dragging every row into C#. Open()
        // registers these on every connection handed out here, pooled ones included.
        // CreateFunction defaults to isDeterministic: false, so determinism is passed explicitly.
        connection.CreateFunction(
            "like",
            (string? pattern, string? text) => Like(pattern, text, escape: null),
            isDeterministic: true);
        connection.CreateFunction(
            "like",
            (string? pattern, string? text, string? escape) => Like(pattern, text, escape),
            isDeterministic: true);

        connection.Open();
        return connection;
    }

    /// <summary>
    /// Unicode-aware <c>like(pattern, text[, escape])</c>: % and _ are wildcards, the escape
    /// character makes the next character literal, and folding follows the invariant culture
    /// per the app's OrdinalIgnoreCase contract. NULL operands are false.
    /// </summary>
    private static bool Like(string? pattern, string? text, string? escape)
    {
        if (pattern is null || text is null)
        {
            return false;
        }

        // Regex.IsMatch runs through the runtime's bounded static cache (Regex.CacheSize,
        // 15 entries, least-recently-used dropped) instead of a dictionary that would keep
        // a compiled Regex for every term ever searched for the app's lifetime.
        return Regex.IsMatch(
            text,
            TranslateLikePattern(pattern, escape),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
            TimeSpan.FromSeconds(1));
    }

    /// <summary>Turns a LIKE pattern into an anchored Regex source.</summary>
    private static string TranslateLikePattern(string pattern, string? escape)
    {
        char? escapeCharacter = string.IsNullOrEmpty(escape) ? null : escape[0];
        var source = new StringBuilder(pattern.Length + 8).Append(@"\A");

        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c != escapeCharacter && c == '%')
            {
                source.Append(".*");
            }
            else if (c != escapeCharacter && c == '_')
            {
                source.Append('.');
            }
            else if (c == escapeCharacter && i + 1 < pattern.Length)
            {
                // The character after the escape character is always literal — including
                // %, _ and the escape character itself.
                source.Append(Regex.Escape(pattern[++i].ToString()));
            }
            else
            {
                source.Append(Regex.Escape(c.ToString()));
            }
        }

        return source.Append(@"\z").ToString();
    }
}
