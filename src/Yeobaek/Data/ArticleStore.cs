using System.Text;
using Microsoft.Data.Sqlite;

namespace Yeobaek.Data;

public sealed record ArchivedArticle(
    long Id, string Url, string Title, string? Byline, string? Site, string Folder, long SizeBytes, DateTime SavedAt)
{
    public string SizeLabel => SizeBytes >= 1024 * 1024
        ? $"{SizeBytes / 1024d / 1024d:0.0}MB"
        : $"{Math.Max(1, SizeBytes / 1024)}KB";
}

/// <summary>검색 결과 한 건. Snippet 은 본문에서 검색어가 처음 나온 곳 주변(검색어가 없으면 본문 앞부분).</summary>
public sealed record ArticleSearchResult(ArchivedArticle Article, string Snippet)
{
    // 목록 창에서 바로 바인딩할 수 있게 자주 쓰는 값을 꺼내 둔다
    public string Title => Article.Title;
    public string Site => Article.Site ?? HostName.FromUrl(Article.Url) ?? "";
    public DateTime SavedAt => Article.SavedAt;
    public string SizeLabel => Article.SizeLabel;
}

/// <summary>
/// 보관한 글의 목록과 전문 검색 색인(articles, articles_fts). 글 파일은 ArchiveService 가 관리한다.
/// </summary>
public sealed class ArticleStore(Database database)
{
    private const int MaxSearchTerms = 8;
    private const int SnippetLength = 120;

    /// <summary>글을 저장하거나(같은 주소면) 바꾼다. 검색 색인도 함께 바꾼다.</summary>
    public ArchivedArticle Save(string url, string title, string? byline, string? site, string folder, long sizeBytes, string plainText)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();

        var savedAt = Database.Now();
        var id = Convert.ToInt64(connection.Command(transaction, """
            INSERT INTO articles(url, title, byline, site, folder, size_bytes, saved_at)
            VALUES($url, $title, $byline, $site, $folder, $size, $savedAt)
            ON CONFLICT(url) DO UPDATE SET
              title = excluded.title, byline = excluded.byline, site = excluded.site,
              folder = excluded.folder, size_bytes = excluded.size_bytes, saved_at = excluded.saved_at
            RETURNING id;
            """,
            ("$url", url), ("$title", title), ("$byline", byline), ("$site", site),
            ("$folder", folder), ("$size", sizeBytes), ("$savedAt", savedAt)).ExecuteScalar());

        // 색인은 글 id 를 rowid 로 쓴다. 다시 보관하면 옛 색인을 지우고 새로 넣는다.
        connection.Command(transaction, "DELETE FROM articles_fts WHERE rowid = $id", ("$id", id)).ExecuteNonQuery();
        connection.Command(transaction, "INSERT INTO articles_fts(rowid, title, body) VALUES($id, $title, $body)",
            ("$id", id), ("$title", title), ("$body", CollapseWhitespace(plainText))).ExecuteNonQuery();

        transaction.Commit();
        return new ArchivedArticle(id, url, title, byline, site, folder, sizeBytes, Database.ParseTime(savedAt));
    }

    public ArchivedArticle? FindByFolder(string folder)
        => QueryArticles("SELECT id, url, title, byline, site, folder, size_bytes, saved_at FROM articles WHERE folder = $folder",
            ("$folder", folder)).FirstOrDefault();

    public ArchivedArticle? FindByUrl(string url)
        => QueryArticles("SELECT id, url, title, byline, site, folder, size_bytes, saved_at FROM articles WHERE url = $url",
            ("$url", url)).FirstOrDefault();

    /// <summary>
    /// 제목·본문에 검색어가 모두 들어 있는 글을 최근에 보관한 순으로 찾는다. 빈 검색어면 전부.
    /// 검색어는 띄어쓰기로 나누고, 각 검색어는 단어 중간에 있어도 찾는다(2글자도 가능).
    /// </summary>
    public List<ArticleSearchResult> Search(string query, int limit = 200)
    {
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSearchTerms)
            .ToList();

        var sql = new StringBuilder("""
            SELECT a.id, a.url, a.title, a.byline, a.site, a.folder, a.size_bytes, a.saved_at, f.body
            FROM articles a JOIN articles_fts f ON f.rowid = a.id
            """);
        var parameters = new List<(string, object?)> { ("$limit", limit) };
        for (var i = 0; i < terms.Count; i++)
        {
            var (pattern, escape) = LikePattern(terms[i]);
            sql.Append(i == 0 ? " WHERE " : " AND ")
               .Append($"(f.title LIKE $t{i}{escape} OR f.body LIKE $t{i}{escape})");
            parameters.Add(($"$t{i}", pattern));
        }
        sql.Append(" ORDER BY a.saved_at DESC LIMIT $limit");

        using var connection = database.Open();
        using var reader = connection.Command(sql.ToString(), parameters.ToArray()).ExecuteReader();
        var results = new List<ArticleSearchResult>();
        while (reader.Read())
            results.Add(new ArticleSearchResult(ReadArticle(reader), MakeSnippet(reader.GetString(8), terms)));
        return results;
    }

    public void Delete(long id)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        connection.Command(transaction, "DELETE FROM articles_fts WHERE rowid = $id", ("$id", id)).ExecuteNonQuery();
        connection.Command(transaction, "DELETE FROM articles WHERE id = $id", ("$id", id)).ExecuteNonQuery();
        transaction.Commit();
    }

    public (int Count, long TotalBytes) Summary()
    {
        using var connection = database.Open();
        using var reader = connection.Command("SELECT COUNT(*), COALESCE(SUM(size_bytes), 0) FROM articles").ExecuteReader();
        reader.Read();
        return (reader.GetInt32(0), reader.GetInt64(1));
    }

    /// <summary>
    /// LIKE 의 와일드카드(%, _)와 이스케이프 문자는 글자 그대로 찾도록 이스케이프한다.
    /// ESCAPE 절은 필요할 때만 붙인다(없어야 trigram 색인이 LIKE 를 처리한다).
    /// </summary>
    private static (string Pattern, string EscapeClause) LikePattern(string term)
    {
        if (term.IndexOfAny(['%', '_', '\\']) < 0) return ($"%{term}%", "");
        var escaped = term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return ($"%{escaped}%", " ESCAPE '\\'");
    }

    private static string MakeSnippet(string body, IReadOnlyList<string> terms)
    {
        var first = terms
            .Select(term => body.IndexOf(term, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(0)
            .Min();
        var start = Math.Max(0, first - SnippetLength / 3);
        var length = Math.Min(SnippetLength, body.Length - start);
        var snippet = body.Substring(start, length);
        return (start > 0 ? "…" : "") + snippet + (start + length < body.Length ? "…" : "");
    }

    private static string CollapseWhitespace(string text)
        => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private List<ArchivedArticle> QueryArticles(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = database.Open();
        using var reader = connection.Command(sql, parameters).ExecuteReader();
        var articles = new List<ArchivedArticle>();
        while (reader.Read()) articles.Add(ReadArticle(reader));
        return articles;
    }

    private static ArchivedArticle ReadArticle(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetString(5),
        reader.GetInt64(6),
        Database.ParseTime(reader.GetString(7)));
}
