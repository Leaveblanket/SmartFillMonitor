using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Session;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SmartFillMonitor.Tests;

internal sealed class TestAppScope : IDisposable
{
    public string DbPath { get; }

    public IAppDbContext DbContext { get; }

    public ISessionService Session { get; }

    public IAuditService AuditService { get; }

    public IAuthorizationService AuthorizationService { get; }

    public IUserService UserService { get; }

    public IAlarmService AlarmService { get; }

    public IProductionRecordService ProductionRecordService { get; }

    public IConfigService ConfigService { get; }

    public TestAppScope()
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"smartfill-practice-test-{Guid.NewGuid():N}.db");

        var dbContext = new AppDbContext();
        dbContext.Initialize($"Data Source={DbPath}");
        DbContext = dbContext;

        Session = new SessionService();
        AuditService = new AuditService();
        AuthorizationService = new AuthorizationService(Session, AuditService);
        var exportService = new CsvExportService();
        new AlarmBootstrapper(DbContext).InitializeAsync().GetAwaiter().GetResult();
        new ProductionRecordBootstrapper(DbContext).InitializeAsync().GetAwaiter().GetResult();

        var productionRecordService = new ProductionRecordService(DbContext, AuditService, exportService);

        UserService = new UserService(DbContext, Session, AuditService);
        var alarmService = new AlarmService(DbContext, Session, AuthorizationService, AuditService);
        AlarmService = alarmService;
        ProductionRecordService = productionRecordService;
        ConfigService = new ConfigService(AuditService);
    }

    public void SetCurrentUser(User user)
    {
        Session.SetCurrentUser(user);
    }

    public User GetUser(string userName)
    {
        return DbContext.Fsql.Select<User>().Where(x => x.UserName == userName).First()!;
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(DbPath))
            {
                File.Delete(DbPath);
            }
        }
        catch
        {
        }
    }
}

internal static class StaTestHelper
{
    private static readonly object Gate = new();
    private static Thread? _staThread;
    private static Dispatcher? _dispatcher;
    private static Application? _application;

    public static Application CurrentApplication =>
        _application ?? throw new InvalidOperationException("WPF Application has not been initialized.");

    public static Task RunAsync(Action action)
    {
        EnsureDispatcher();

        var tcs = new TaskCompletionSource<object?>();
        _dispatcher!.BeginInvoke(new Action(() =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }));

        return tcs.Task;
    }

    private static void EnsureDispatcher()
    {
        if (_dispatcher != null && !_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
        {
            return;
        }

        lock (Gate)
        {
            if (_dispatcher != null && !_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
            {
                return;
            }

            _dispatcher = null;
            _application = null;

            var ready = new TaskCompletionSource<Dispatcher>();
            _staThread = new Thread(() =>
            {
                if (Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted || Application.Current.Dispatcher.HasShutdownFinished)
                {
                    _application = new Application
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown
                    };
                }
                else
                {
                    _application = Application.Current;
                }

                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                ready.SetResult(Dispatcher.CurrentDispatcher);
                Dispatcher.Run();
            });

            _staThread.SetApartmentState(ApartmentState.STA);
            _staThread.IsBackground = true;
            _staThread.Start();
            _dispatcher = ready.Task.GetAwaiter().GetResult();
        }
    }
}

internal static class WpfTestResources
{
    public static void EnsureMainShellResources()
    {
        var application = StaTestHelper.CurrentApplication;
        application.Resources["AppBgDark"] ??= new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        application.Resources["AppBgPanel"] ??= new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
        application.Resources["AppBgHeader"] ??= new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
        application.Resources["AppBoderBrush"] ??= new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42));
        application.Resources["AppAccentColor"] ??= new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
        application.Resources["AppTextMain"] ??= new SolidColorBrush(Colors.White);
        application.Resources["AppTextMuted"] ??= new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        application.Resources["BooleanToVisibilityConverter"] ??= new BooleanToVisibilityConverter();

        if (!application.Resources.Contains("OutLineButton"))
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.ForegroundProperty, application.Resources["AppTextMain"]));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, application.Resources["AppBoderBrush"]));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            application.Resources["OutLineButton"] = style;
        }

        if (!application.Resources.Contains("NavItemStyle"))
        {
            var style = new Style(typeof(RadioButton));
            style.Setters.Add(new Setter(Control.ForegroundProperty, application.Resources["AppTextMuted"]));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(3, 0, 0, 0)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(20, 15, 20, 15)));
            application.Resources["NavItemStyle"] = style;
        }

        if (!application.Resources.Contains("CloseBtnStyle"))
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
            application.Resources["CloseBtnStyle"] = style;
        }
    }

    public static void EnsureAuthShellResources()
    {
        var application = StaTestHelper.CurrentApplication;
        application.Resources["BooleanToVisibilityConverter"] ??= new BooleanToVisibilityConverter();
        application.Resources["PrimaryBlue"] ??= new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
        application.Resources["PrimaryBlueHover"] ??= new SolidColorBrush(Color.FromRgb(0x00, 0x94, 0xF5));
        application.Resources["DarkBg"] ??= new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));
        application.Resources["CardBg"] ??= new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x35));
        application.Resources["TextDim"] ??= new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        application.Resources["InputBg"] ??= new SolidColorBrush(Color.FromRgb(0x2F, 0x2F, 0x41));

        if (!application.Resources.Contains("InputBorderStyle"))
        {
            var style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.BackgroundProperty, application.Resources["InputBg"]));
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(6)));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 40d));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 12)));
            style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.Transparent));
            application.Resources["InputBorderStyle"] = style;
        }

        if (!application.Resources.Contains("LoginInputBorderStyle"))
        {
            var style = new Style(typeof(Border), (Style)application.Resources["InputBorderStyle"]);
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 15)));
            application.Resources["LoginInputBorderStyle"] = style;
        }

        if (!application.Resources.Contains("MordenBtnStyle"))
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
            application.Resources["MordenBtnStyle"] = style;
        }

        if (!application.Resources.Contains("CloseBtnStyle"))
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
            application.Resources["CloseBtnStyle"] = style;
        }
    }
}

internal sealed class AuthShellAutoAccepter : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Action? _onAccept;
    private readonly HashSet<Window> _handledWindows = new();

    public AuthShellAutoAccepter(Action? onAccept = null)
    {
        _onAccept = onAccept;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var application = Application.Current;
        if (application == null)
        {
            return;
        }

        foreach (Window window in application.Windows)
        {
            if (window is SmartFillMonitor.Views.Auth.AuthShellView authShell &&
                authShell.IsVisible &&
                !_handledWindows.Contains(authShell))
            {
                _handledWindows.Add(authShell);
                _onAccept?.Invoke();
                authShell.DialogResult = true;
            }
        }
    }
}

internal sealed class AuthShellAutoRejector : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly HashSet<Window> _handledWindows = new();

    public AuthShellAutoRejector()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var application = Application.Current;
        if (application == null)
        {
            return;
        }

        foreach (Window window in application.Windows)
        {
            if (window is SmartFillMonitor.Views.Auth.AuthShellView authShell &&
                authShell.IsVisible &&
                !_handledWindows.Contains(authShell))
            {
                _handledWindows.Add(authShell);
                authShell.DialogResult = false;
            }
        }
    }
}
