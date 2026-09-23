using Microsoft.Data.Sqlite;
using Yeobaek.Data;

namespace Yeobaek.Tests;

public sealed class ArticleStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"yeobaek-test-{Guid.NewGuid():N}.db");
    private readonly ArticleStore _store;

    public ArticleStoreTests()
    {
        var database = new Database(_path);
        database.EnsureCreated(RuleStore.SeedGlobalRules);
        _store = new ArticleStore(database);
    }

    [Fact]
    public void Search_finds_short_korean_words_inside_longer_words_and_requires_every_term()
    {
        Save("https://a.test/1", "브라우저 만들기", "광고 없는 전문검색 기능을 붙였습니다.");
        Save("https://a.test/2", "요리 일기", "오늘은 검색 대신 산책을 했다.");

        Assert.Equal(["브라우저 만들기", "요리 일기"], Titles("검색"));   // 2글자, "전문검색" 속에서도 찾음
        Assert.Equal(["브라우저 만들기"], Titles("검색 광고"));             // 모든 낱말이 들어간 글만
        Assert.Equal(["요리 일기"], Titles("요리"));                        // 제목에서도 찾음
    }

    [Fact]
    public void Search_treats_like_wildcards_as_plain_text()
    {
        Save("https://a.test/1", "할인", "50% 할인");
        Save("https://a.test/2", "파일", "file_name 규칙");
        Save("https://a.test/3", "다른 글", "아무 내용");

        Assert.Equal(["할인"], Titles("%"));
        Assert.Equal(["파일"], Titles("_"));
    }

    [Fact]
    public void Resaving_same_url_replaces_the_index_and_delete_removes_it()
    {
        Save("https://a.test/1", "처음 제목", "옛날 본문");
        var resaved = Save("https://a.test/1", "바뀐 제목", "새로운 본문");

        Assert.Empty(Titles("옛날"));
        Assert.Equal(["바뀐 제목"], Titles("새로운"));

        _store.Delete(resaved.Id);
        Assert.Empty(Titles(""));
    }

    private ArchivedArticle Save(string url, string title, string body)
        => _store.Save(url, title, null, null, folder: url.GetHashCode().ToString("x"), sizeBytes: 1, body);

    private List<string> Titles(string query) => _store.Search(query).Select(result => result.Title).Order().ToList();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }
}
