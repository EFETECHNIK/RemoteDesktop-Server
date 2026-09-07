using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Ekran akışı ve komutlar için 10MB mesaj sınırı
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
});

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

var app = builder.Build();

app.UseCors();

// Test ve sağlık kontrolü
app.MapGet("/", () => "EFETECHNIK Sunucusu Aktif!");

// İstemcilerin bağlanacağı dağıtım ucu
app.MapHub<RelayHub>("/relayHub");

app.Run();

public class RelayHub : Hub
{
    private static readonly Dictionary<string, string> OnlineDevices = new();

    public Task RegisterDevice(string deviceId)
    {
        lock (OnlineDevices)
        {
            OnlineDevices[deviceId] = Context.ConnectionId;
        }
        return Clients.Caller.SendAsync("RegistrationSuccess", deviceId);
    }

    public async Task RequestConnection(string targetDeviceId, string requesterName)
    {
        string? targetConnectionId;
        lock (OnlineDevices)
        {
            OnlineDevices.TryGetValue(targetDeviceId, out targetConnectionId);
        }

        if (!string.IsNullOrEmpty(targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("IncomingConnectionRequest", Context.ConnectionId, requesterName);
        }
        else
        {
            await Clients.Caller.SendAsync("ConnectionFailed", "Cihaz çevrim dışı veya bulunamadı.");
        }
    }

    public Task RespondConnection(string requesterConnectionId, bool approved)
    {
        return Clients.Client(requesterConnectionId).SendAsync("ConnectionResponse", approved, Context.ConnectionId);
    }

    public Task SendScreenFrame(string targetConnectionId, byte[] frameData)
    {
        return Clients.Client(targetConnectionId).SendAsync("ReceiveScreenFrame", frameData);
    }

    public Task SendInputEvent(string targetConnectionId, string command)
    {
        return Clients.Client(targetConnectionId).SendAsync("ReceiveInputEvent", command);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        lock (OnlineDevices)
        {
            var item = OnlineDevices.FirstOrDefault(kvp => kvp.Value == Context.ConnectionId);
            if (!string.IsNullOrEmpty(item.Key))
            {
                OnlineDevices.Remove(item.Key);
            }
        }
        return base.OnDisconnectedAsync(exception);
    }
}