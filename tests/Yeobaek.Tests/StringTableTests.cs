using Yeobaek.Ui;

namespace Yeobaek.Tests;

public sealed class StringTableTests
{
    [Fact]
    public void EveryMessageHasAllFourTranslations()
    {
        foreach (var (key, translations) in StringTable.GetEntries())
        {
            foreach (var language in StringTable.Languages)
            {
                Assert.True(translations.TryGetValue(language, out var text) && !string.IsNullOrWhiteSpace(text),
                    $"Missing {language} translation: {key}");
                Assert.Null(Record.Exception(() => string.Format(text!, DateTime.Today, 1, 2)));
            }
        }
    }

    [Fact]
    public void UnsupportedLanguageFallsBackToKorean()
    {
        Assert.Equal("새 탭", StringTable.Get("fr", "Main.NewTab"));
    }

    [Fact]
    public void EnglishMessageUsesEnglishTable()
    {
        Assert.Equal("New tab", StringTable.Get("en", "Main.NewTab"));
    }
}
