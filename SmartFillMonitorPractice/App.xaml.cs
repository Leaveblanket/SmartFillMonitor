using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SmartFillMonitor.Services.Alarms;
using SmartFillMonitor.Services.Configuration;
using SmartFillMonitor.Services.Dialogs;
using SmartFillMonitor.Services.Logging;
using SmartFillMonitor.Services.Navigation;
using SmartFillMonitor.Services.Persistence;
using SmartFillMonitor.Services.Plc;
using SmartFillMonitor.Services.Security;
using SmartFillMonitor.Services.Session;
using SmartFillMonitor.Services.Shared;
using SmartFillMonitor.Services.Threading;
using SmartFillMonitor.Services.Simulation;
using SmartFillMonitor.ViewModels.Auth;
using SmartFillMonitor.ViewModels.Main;
using SmartFillMonitor.Views.Auth;
using SmartFillMonitor.Views.Main;

namespace SmartFillMonitor
{
    public partial class App : Application
    {
        private const string LogTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss fff} [{Level}] ({ThreadId}) {Message}{NewLine}{Exception}";
        private const string LogPath = "Logs\\log-.txt";
        private static readonly string DbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartFillMonitor.db");
        private static readonly string DbConnectionString = $"Data Source={DbFilePath}";

        public IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SetExceptionHandling(); // 设置全局异常处理

            try
            {
                ConfigureServices(); // 注册服务
                ConfigureLogging(); // 配置日志记录

                await InitializeCoreServicesAsync(); // 初始化核心服务

                // 启动身份验证流程
                var coordinator = ServiceProvider.GetRequiredService<ISessionCoordinator>();
                var started = await coordinator.StartAsync();
                if (!started)
                {
                    LogHelper.Info("登录窗口在完成身份验证前已关闭，应用即将退出。");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Fatal("应用启动失败。", ex);
                MessageBox.Show($"应用程序启动失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (ServiceProvider != null)
                {
                    ServiceProvider.GetRequiredService<IPlcAlarmMonitorService>()
                        .StopAsync()
                        .GetAwaiter()
                        .GetResult();
                }

                if (ServiceProvider is IAsyncDisposable asyncDisposable)
                {
                    asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                else if (ServiceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("应用退出时释放服务提供程序失败。", ex);
            }

            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private void SetExceptionHandling()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                LogHelper.Error($"发生未处理的界面异常。窗口={Current?.MainWindow?.GetType().Name ?? "无"}", e.Exception);

#if DEBUG
                if (Debugger.IsAttached)
                {
                    return;
                }
#endif

                e.Handled = true;
                MessageBox.Show($"UI 异常：{e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LogHelper.Fatal($"发生未处理的非界面异常。IsTerminating={e.IsTerminating}", ex);
                }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogHelper.Error("发现未观察到的任务异常。", e.Exception);
                e.SetObserved();
            };
        }

        private async Task InitializeCoreServicesAsync()
        {
            Log.Debug("正在初始化核心服务。");

            var dbContext = ServiceProvider.GetRequiredService<IAppDbContext>();
            dbContext.Initialize(DbConnectionString);

            await ServiceProvider.GetRequiredService<IAlarmBootstrapper>().InitializeAsync();
            await ServiceProvider.GetRequiredService<IProductionRecordBootstrapper>().InitializeAsync();
            LogHelper.Info("核心服务初始化成功。");
        }

        #region Service Registration
        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            RegisterAppServices(services);
            RegisterSessionServices(services);
            RegisterAuthenticationServices(services);

            ServiceProvider = services.BuildServiceProvider();
        }

        private static void RegisterAppServices(IServiceCollection services)
        {
            services.AddSingleton<IAppDbContext, AppDbContext>();

            services.AddSingleton<IConfigService, ConfigService>();

            services.AddSingleton<ISessionCoordinator, SessionCoordinator>();

            services.AddSingleton<IExportService, CsvExportService>();
            services.AddSingleton<ISystemLogService, SystemLogService>();
            services.AddSingleton<ILogLiveFeed, LogLiveFeed>();

            services.AddSingleton<IDialogService, DialogService>();

            services.AddSingleton<IUiThreadService, WpfUiThreadService>();
        }

        private static void RegisterSessionServices(IServiceCollection services)
        {
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IAuditService, AuditService>();
            services.AddSingleton<IAuthorizationService, AuthorizationService>();
            services.AddSingleton<IUserService, UserService>();

            services.AddSingleton<IAlarmBootstrapper, AlarmBootstrapper>();
            services.AddSingleton<IAlarmService, AlarmService>();
            services.AddSingleton<IPlcAlarmMonitorService, PlcAlarmMonitorService>();

            services.AddSingleton<IProductionRecordBootstrapper, ProductionRecordBootstrapper>();
            services.AddSingleton<IProductionRecordService, ProductionRecordService>();
            services.AddSingleton<IProductionRunService, ProductionRunService>();

            services.AddSingleton<ISerialPortService, SerialPortService>();
            services.AddSingleton<IPlcTransport, ModbusRtuTransport>();
            services.AddSingleton<IPlcService, PlcService>();

            services.AddScoped<IMainNavigationService>(sp => new NavigationService(
                sp,
                sp.GetRequiredService<ISessionService>(),
                sp.GetRequiredService<SimulationViewModel>()));

            services.AddScoped<HeaderViewModel>();
            services.AddScoped<AlarmsViewModel>();
            services.AddScoped<DashBoardViewModel>();
            services.AddScoped<SimulationViewModel>();
            services.AddScoped<DashQueryViewModel>();
            services.AddScoped<LogsViewModel>();
            services.AddScoped<SettingsViewModel>();
            services.AddScoped<MainViewModel>();
            services.AddScoped<MainView>();
            services.AddScoped<AccountSecurityViewModel>();
        }

        private static void RegisterAuthenticationServices(IServiceCollection services)
        {
            services.AddSingleton<IAuthNavigationService>(sp => new NavigationService(sp));
            services.AddTransient<AuthShellViewModel>();
            services.AddTransient<AuthShellView>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();
        }

        #endregion

        #region Logging Configuration
        private void ConfigureLogging()
        {
            var liveFeed = ServiceProvider.GetRequiredService<ILogLiveFeed>();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithThreadId()
                .WriteTo.Sink(new LiveFeedSink(liveFeed, LogTemplate))
                .WriteTo.Console(outputTemplate: LogTemplate)
                .WriteTo.File(LogPath, rollingInterval: RollingInterval.Day, outputTemplate: LogTemplate, shared: true)
                .WriteTo.SQLite(DbFilePath, tableName: "SystemLog", storeTimestampInUtc: false)
                .CreateLogger();
        }
        #endregion
    }
}
