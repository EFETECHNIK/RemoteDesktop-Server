using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

builder.Services.AddSignalR(hubOptions =>
{
    hubOptions.MaximumReceiveMessageSize = 100 * 1024 * 1024; // Dosya aktarımı için 100MB yapıldı
    hubOptions.EnableDetailedErrors = true;
});

var app = builder.Build();

app.UseCors();
app.MapHub<RelayHub>("/relayHub");
app.MapGet("/", () => "EFETECHNIK Remote Desktop Relay Server Aktif!");

app.Run();

public class RelayHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> DeviceConnections = new();
    private static readonly ConcurrentDictionary<string, string> ConnectionDevices = new();

    public Task RegisterDevice(string deviceId)
    {
        DeviceConnections[deviceId] = Context.ConnectionId;
        ConnectionDevices[Context.ConnectionId] = deviceId;
        return Task.CompletedTask;
    }

    public async Task RequestConnection(string targetDeviceId)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            var senderId = ConnectionDevices.TryGetValue(Context.ConnectionId, out var devId) ? devId : "Bilinmeyen";
            await Clients.Client(targetConnectionId).SendAsync("ReceiveConnectionRequest", senderId);
        }
        else
        {
            await Clients.Caller.SendAsync("ConnectionRejected", "Hedef cihaz bulunamadı.");
        }
    }

    public async Task AcceptConnection(string requesterDeviceId)
    {
        if (DeviceConnections.TryGetValue(requesterDeviceId, out var requesterConnectionId))
        {
            var myDeviceId = ConnectionDevices.TryGetValue(Context.ConnectionId, out var devId) ? devId : "Host";
            await Clients.Client(requesterConnectionId).SendAsync("ConnectionAccepted", myDeviceId);
        }
    }

    public async Task RejectConnection(string requesterDeviceId)
    {
        if (DeviceConnections.TryGetValue(requesterDeviceId, out var requesterConnectionId))
        {
            var myDeviceId = ConnectionDevices.TryGetValue(Context.ConnectionId, out var devId) ? devId : "Host";
            await Clients.Client(requesterConnectionId).SendAsync("ConnectionRejected", myDeviceId);
        }
    }

    public async Task SendScreenFrame(string targetDeviceId, byte[] frameData)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveScreenFrame", frameData);
        }
    }

    public async Task SendInputEvent(string targetDeviceId, string actionType, double normX, double normY, string btn)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveInputEvent", actionType, normX, normY, btn);
        }
    }

    public async Task SendKeyEvent(string targetDeviceId, string eventType, int vkCode)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveKeyEvent", eventType, vkCode);
        }
    }

    public async Task SendFilePayload(string targetDeviceId, string fileName, byte[] fileBytes)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveFilePayload", fileName, fileBytes);
        }
    }

    public async Task DisconnectSession(string targetDeviceId)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("SessionTerminated");
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionDevices.TryRemove(Context.ConnectionId, out var deviceId))
        {
            DeviceConnections.TryRemove(deviceId, out _);
        }
        return base.OnDisconnectedAsync(exception);
    }
}
