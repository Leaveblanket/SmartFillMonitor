using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services;
using SmartFillMonitor.ViewModels.Main;
using Xunit;

namespace SmartFillMonitor.Tests;

public class HeaderViewModelTests
{
    [Fact]
    public void FakeDialogService_Records_Last_Message_And_Returns_Configured_Result()
    {
        var dialog = new FakeDialogService
        {
            Result = DialogResult.Yes
        };

        var result = dialog.ShowMessage(
            "确定要退出系统吗？",
            "退出确认",
            PromptSeverity.Information,
            DialogButtons.YesNo);

        Assert.Equal(DialogResult.Yes, result);
        Assert.Equal(1, dialog.ShowMessageCallCount);
        Assert.Equal("确定要退出系统吗？", dialog.LastMessage);
        Assert.Equal("退出确认", dialog.LastCaption);
        Assert.Equal(PromptSeverity.Information, dialog.LastSeverity);
        Assert.Equal(DialogButtons.YesNo, dialog.LastButtons);
    }

    [Fact]
    public async Task Activate_UsesDashboardAndFallbackSourcesCorrectly()
    {
        await StaTestHelper.RunAsync(() =>
        {
            var plcService = new FakePlcService
            {
                Snapshot = new PlcReadSnapshot
                {
                    HasSuccessfulRead = true,
                    LastDeviceState = new DeviceState { BarCode = "PLC-001" }
                }
            };
            var dialog = new FakeDialogService();
            var uiThread = new ImmediateUiThreadService();
            var dashboard = new DashBoardViewModel(
                plcService,
                new FakeAlarmService(),
                new FakeProductionRunService(),
                dialog,
                uiThread)
            {
                IndicatorState = LightState.Green
            };
            var simulation = new SimulationViewModel(
                plcService,
                dialog,
                uiThread)
            {
                IndicatorState = LightState.Yellow,
                CurrentBatchNo = "SIM-001"
            };

            using var header = new HeaderViewModel(plcService, uiThread);

            header.Activate(dashboard);
            Assert.Equal(LightState.Green, header.IndicatorState);
            Assert.Equal("PLC-001", header.CurrentBatchNo);

            header.Activate(simulation);
            Assert.Equal(LightState.Yellow, header.IndicatorState);
            Assert.Equal("SIM-001", header.CurrentBatchNo);

            header.Activate(new object());
            Assert.Equal(LightState.Yellow, header.IndicatorState);
            Assert.Equal("PLC-001", header.CurrentBatchNo);

            dashboard.Dispose();
            simulation.Dispose();
        });
    }

    [Fact]
    public void DataReceived_UpdatesBatch_WhenNotOnSimulationPage_AndStopPreventsFurtherChanges()
    {
        var plcService = new FakePlcService();
        var uiThread = new ImmediateUiThreadService();
        using var header = new HeaderViewModel(plcService, uiThread);

        header.Activate(new object());
        plcService.RaiseDataReceived(new DeviceState { BarCode = "BATCH-100" });
        Assert.Equal("BATCH-100", header.CurrentBatchNo);

        header.Stop();
        plcService.RaiseDataReceived(new DeviceState { BarCode = "BATCH-200" });
        Assert.Equal("BATCH-100", header.CurrentBatchNo);
    }

    [Fact]
    public async Task HeaderViewModel_Still_Updates_Content_After_UiThreadService_Replaces_RunOnUi()
    {
        await StaTestHelper.RunAsync(() =>
        {
            var plcService = new FakePlcService
            {
                Snapshot = new PlcReadSnapshot
                {
                    HasSuccessfulRead = true,
                    LastDeviceState = new DeviceState { BarCode = "PLC-001" }
                }
            };
            var dialog = new FakeDialogService();
            var uiThread = new ImmediateUiThreadService();
            var dashboard = new DashBoardViewModel(
                plcService,
                new FakeAlarmService(),
                new FakeProductionRunService(),
                dialog,
                uiThread)
            {
                IndicatorState = LightState.Green
            };
            using var header = new HeaderViewModel(plcService, uiThread);

            header.Activate(dashboard);

            Assert.Equal(LightState.Green, header.IndicatorState);
            Assert.True(uiThread.BeginInvokeCallCount > 0 || uiThread.InvokeCallCount > 0);

            dashboard.Dispose();
        });
    }
}
