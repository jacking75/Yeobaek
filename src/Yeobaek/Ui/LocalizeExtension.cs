using System.Windows.Markup;

namespace Yeobaek.Ui;

/// <summary>XAML에서 문자열 표의 키를 간단히 참조한다.</summary>
public sealed class LocalizeExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => StringTable.Get(Key);
}
