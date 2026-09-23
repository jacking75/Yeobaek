namespace Yeobaek.Data;

public sealed record QueueEntry(long Id, string Url, string? Title, DateTime AddedAt, DateTime? ReadAt)
{
    public bool IsRead => ReadAt is not null;
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Url : Title;
    public string Site => HostName.FromUrl(Url) ?? "";
}

/// <summary>나중에 읽기 목록.</summary>
public sealed class ReadingQueue(Database database)
{
    /// <summary>
    /// 글을 담는다. 새로 담았거나 읽은 글을 다시 담으면 true, 이미 읽지 않은 상태로 있으면 false.
    /// </summary>
    public bool Add(string url, string? title)
    {
        title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        using var connection = database.Open();

        var existingReadAt = connection.Command("SELECT read_at FROM queue WHERE url = $url", ("$url", url))
            .ExecuteScalar();
        if (existingReadAt is null)
        {
            connection.Command("INSERT INTO queue(url, title, added_at) VALUES($url, $title, $now)",
                ("$url", url), ("$title", title), ("$now", Database.Now())).ExecuteNonQuery();
            return true;
        }

        connection.Command("UPDATE queue SET read_at = NULL, title = COALESCE($title, title) WHERE url = $url",
            ("$url", url), ("$title", title)).ExecuteNonQuery();
        return existingReadAt is not DBNull;
    }

    public List<QueueEntry> GetAll(bool unreadOnly)
    {
        var sql = "SELECT id, url, title, added_at, read_at FROM queue"
                  + (unreadOnly ? " WHERE read_at IS NULL" : "")
                  + " ORDER BY added_at DESC";

        using var connection = database.Open();
        using var reader = connection.Command(sql).ExecuteReader();
        var entries = new List<QueueEntry>();
        while (reader.Read())
        {
            entries.Add(new QueueEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                Database.ParseTime(reader.GetString(3)),
                reader.IsDBNull(4) ? null : Database.ParseTime(reader.GetString(4))));
        }
        return entries;
    }

    public int CountUnread()
    {
        using var connection = database.Open();
        return Convert.ToInt32(connection.Command("SELECT COUNT(*) FROM queue WHERE read_at IS NULL").ExecuteScalar());
    }

    public void SetRead(long id, bool read)
    {
        using var connection = database.Open();
        connection.Command("UPDATE queue SET read_at = $readAt WHERE id = $id",
            ("$readAt", read ? Database.Now() : null), ("$id", id)).ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = database.Open();
        connection.Command("DELETE FROM queue WHERE id = $id", ("$id", id)).ExecuteNonQuery();
    }
}
