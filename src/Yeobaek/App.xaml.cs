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
            AppLog.Error(args.Exception, "관찰되지 않은 작업 예외");
            args.SetObserved();
        };

        AppServices services;
        try
        {
            services = await AppServices.CreateAsync();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowStartupError(
                "Microsoft Edge WebView2 런타임을 찾을 수 없습니다.\n\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/ 에서\n" +
                "'Evergreen 부트스트래퍼'를 내려받아 설치한 뒤 여백을 다시 실행해 주세요.");
            return;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "시작");
            ShowStartupError(
                $"여백을 시작하지 못했습니다.\n\n{ex.Message}\n\n" +
                $"자세한 내용은 다음 파일에 남겼습니다:\n{AppPaths.ErrorLog}");
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
        MessageBox.Show(message, "여백", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }

    /// <summary>예상하지 못한 예외로 앱 전체가 닫히지 않게 한다. 기록을 남기고 사용자에게 알린다.</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error(e.Exception, "처리되지 않은 예외");
        e.Handled = true;

        var message = $"예상하지 못한 문제가 생겼습니다. 계속 사용할 수 있지만 이상하면 여백을 다시 시작해 주세요. ({e.Exception.Message})";
        var window = Windows.OfType<global::Yeobaek.MainWindow>().FirstOrDefault(w => w.IsActive)
                     ?? Windows.OfType<global::Yeobaek.MainWindow>().FirstOrDefault();
        if (window is not null) window.ShowNotice(Notice.Error(message));
        else MessageBox.Show(message, "여백", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
