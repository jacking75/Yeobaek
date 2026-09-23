using Microsoft.Data.Sqlite;

namespace Yeobaek.Data;

/// <summary>
/// rules.db 연결과 스키마. 쿼리는 모두 짧아서 호출마다 연결을 열고 닫는다(드라이버가 풀링한다).
/// </summary>
public sealed class Database(string path)
{
    /// <summary>
    /// 스키마·기본 규칙 버전. 테이블을 더하거나 RuleStore 에 기본 규칙 묶음을 더하면 올린다.
    /// 1: 규칙·예외·나중에 읽기, 2: 쿠팡 파트너스 숨김 규칙, 3: 보관한 글과 전문 검색 색인.
    /// </summary>
    public const int SchemaVersion = 3;

    // 요소 숨김 규칙(host = '*' 는 전역), 차단 예외 사이트, 나중에 읽기, 보관한 글.
    // UNIQUE(host, selector) 가 host 를 앞에 둔 인덱스를 만들므로 host 인덱스는 따로 두지 않는다.
    //
    // 보관한 글의 HTML·이미지는 archive\<folder>\ 에 파일로 두고, DB 에는 목록과 검색 색인만 둔다.
    // 색인은 trigram 토크나이저를 쓴다. unicode61 은 띄어쓰기로만 나눠 "여백은" 속의 "여백",
    // "전문검색" 속의 "검색"을 찾지 못한다. trigram 은 LIKE '%검색어%' 를 색인으로 처리해 준다.
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS rules(
          id       INTEGER PRIMARY KEY,
          host     TEXT NOT NULL,
          selector TEXT NOT NULL,
          added_at TEXT NOT NULL,
          UNIQUE(host, selector)
        );
        CREATE TABLE IF NOT EXISTS allowlist(
          host     TEXT PRIMARY KEY,
          added_at TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS queue(
          id       INTEGER PRIMARY KEY,
          url      TEXT NOT NULL UNIQUE,
          title    TEXT,
          added_at TEXT NOT NULL,
          read_at  TEXT
        );
        CREATE TABLE IF NOT EXISTS articles(
          id         INTEGER PRIMARY KEY,
          url        TEXT NOT NULL UNIQUE,
          title      TEXT NOT NULL,
          byline     TEXT,
          site       TEXT,
          folder     TEXT NOT NULL UNIQUE,
          size_bytes INTEGER NOT NULL,
          saved_at   TEXT NOT NULL
        );
        CREATE VIRTUAL TABLE IF NOT EXISTS articles_fts
          USING fts5(title, body, tokenize = 'trigram');
        """;

    private readonly string _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// 스키마를 만들거나 올린다. seed 에는 이전 버전을 넘겨, 그 뒤에 더해진 기본 규칙만 심게 한다.
    /// 그래야 사용자가 지운 기본 규칙은 되살아나지 않고, 새 기본 규칙은 업데이트 때 들어온다.
    /// </summary>
    public void EnsureCreated(Action<SqliteConnection, SqliteTransaction, int> seed)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        var version = Convert.ToInt32(connection.Command(transaction, "PRAGMA user_version;").ExecuteScalar());
        if (version >= SchemaVersion) return;

        connection.Command(transaction, Schema).ExecuteNonQuery();
        seed(connection, transaction, version);
        connection.Command(transaction, $"PRAGMA user_version = {SchemaVersion};").ExecuteNonQuery();
        transaction.Commit();
    }

    /// <summary>저장 시각 형식. UTC ISO 8601 로 두고 화면에 보일 때 지역 시간으로 바꾼다.</summary>
    public static string Now() => DateTime.UtcNow.ToString("o");

    public static DateTime ParseTime(string value)
        => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime();
}

public static class SqliteCommandExtensions
{
    public static SqliteCommand Command(
        this SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
        => connection.Command(null, sql, parameters);

    public static SqliteCommand Command(
        this SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
}
