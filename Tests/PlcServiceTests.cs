using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using Xunit;

namespace SmartFillMonitor.Tests;

public class PlcServiceTests
{
    [Fact]
    public async Task InitializeAndConnect_PublishesSuccessfulRead()
    {
        var transport = new FakePlcTransport();
        var service = new PlcService(transport, new FakeAuthorizationService(), new AuditService());

        var connectedEvents = 0;
        service.ConnectionChanged += (_, connected) =>
        {
            if (connected)
            {
                connectedEvents++;
            }
        };

        await service.InitializeAsync(new DeviceSettings
        {
            PortName = "COM1",
            BaudRate = 9600,
            DataBits = 8,
            Parity = "None",
            StopBits = "One",
            AutoConnect = true
        });

        await Task.Delay(350);

        Assert.True(service.Snapshot.HasSuccessfulRead);
        Assert.NotNull(service.Snapshot.LastReadSuccessTime);
        Assert.NotNull(service.Snapshot.LastDeviceState);
        Assert.True(connectedEvents >= 1);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task DisconnectAsync_StopsPolling_AndManualDisconnectPreventsReconnect()
    {
        var transport = new FakePlcTransport();
        var service = new PlcService(transport, new FakeAuthorizationService(), new AuditService());
        var dataReceivedCount = 0;
        service.DataReceived += (_, _) => dataReceivedCount++;

        await service.InitializeAsync(new DeviceSettings
        {
            PortName = "COM1",
            BaudRate = 9600,
            DataBits = 8,
            Parity = "None",
            StopBits = "One",
            AutoConnect = true
        });

        await Task.Delay(350);
        var beforeDisconnect = dataReceivedCount;

        await service.DisconnectAsync();
        await Task.Delay(700);

        Assert.False(service.Snapshot.HasSuccessfulRead);
        Assert.Equal(beforeDisconnect, dataReceivedCount);
        Assert.Equal(1, transport.ConnectCount);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task PulseCommandAsync_ReturnsFalse_WhenTransportIsNotConnected()
    {
        var transport = new FakePlcTransport();
        var service = new PlcService(transport, new FakeAuthorizationService(), new AuditService());

        var result = await service.PulseCommandAsync("Start");

        Assert.False(result);

        await service.DisposeAsync();
    }
}


