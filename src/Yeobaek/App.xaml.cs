using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Yeobaek.Data;
using Yeobaek.Ui;

namespace Yeobaek;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error(args.Exception, StringTable.Get("Log.UnobservedTask"));
            args.SetObserved();
        };

        AppServices services;
        try
        {
            services = await AppServices.CreateAsync();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowStartupError(StringTable.Get("App.WebViewMissing"));
            return;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, StringTable.Get("Log.Startup"));
            ShowStartupError(StringTable.Format("App.StartFailed", ex.Message, AppPaths.ErrorLog));
            return;
        }

        // 명령줄로 받은 주소가 있으면 그 주소로, 없으면 시작 페이지로 연다
        var startUrl = e.Args.FirstOrDefault(SafeUrl.IsWebUrl) ?? StartPageOf(services);
        var window = global::Yeobaek.MainWindow.Open(services, startUrl);
        if (services.Settings.Error is { } settingsError) window.ShowNotice(Notice.Error(settingsError));
    }

    public static string? StartPageOf(AppServices services)
        => SafeUrl.IsWebUrl(services.Settings.Current.StartPage) ? services.Settings.Current.StartPage : null;

    private void ShowStartupError(string message)
    {
        MessageBox.Show(message, StringTable.Get("App.Name"), MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }

    /// <summary>예상하지 못한 예외로 앱 전체가 닫히지 않게 한다. 기록을 남기고 사용자에게 알린다.</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error(e.Exception, StringTable.Get("Log.Unhandled"));
        e.Handled = true;

        var message = StringTable.Format("App.UnexpectedError", e.Exception.Message);
        var window = Windows.OfType<global::Yeobaek.MainWindow>().FirstOrDefault(w => w.IsActive)
                     ?? Windows.OfType<global::Yeobaek.MainWindow>().FirstOrDefault();
        if (window is not null) window.ShowNotice(Notice.Error(message));
        else MessageBox.Show(message, StringTable.Get("App.Name"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
