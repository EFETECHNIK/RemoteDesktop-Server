using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// CORS İzinleri (Bulut üzerinden masaüstü erişimi için)
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

// SignalR Servisi (Ekran aktarımı için paket boyut limiti 50MB'a çıkarıldı)
builder.Services.AddSignalR(hubOptions =>
{
    hubOptions.MaximumReceiveMessageSize = 50 * 1024 * 1024;
    hubOptions.EnableDetailedErrors = true;
    hubOptions.KeepAliveInterval = TimeSpan.FromSeconds(10);
    hubOptions.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.UseCors();
app.MapHub<RelayHub>("/relayHub");
app.MapGet("/", () => "EFETECHNIK Remote Desktop Relay Server Aktif!");

app.Run();

// Tüm İletişimi Yöneten Ana Hub
public class RelayHub : Hub
{
    // Cihaz Kimliği (EFETECHNIK-XXXX) ile SignalR ConnectionId Eşleşmesi
    private static readonly ConcurrentDictionary<string, string> DeviceConnections = new();
    private static readonly ConcurrentDictionary<string, string> ConnectionDevices = new();

    // 1. Cihazı Sunucuya Tanıtma
    public Task RegisterDevice(string deviceId)
    {
        DeviceConnections[deviceId] = Context.ConnectionId;
        ConnectionDevices[Context.ConnectionId] = deviceId;
        return Task.CompletedTask;
    }

    // 2. Bağlantı İsteği Gönderme
    public async Task RequestConnection(string targetDeviceId)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            var senderId = ConnectionDevices.TryGetValue(Context.ConnectionId, out var devId) ? devId : "Bilinmeyen Cihaz";
            await Clients.Client(targetConnectionId).SendAsync("ReceiveConnectionRequest", senderId);
        }
        else
        {
            await Clients.Caller.SendAsync("ConnectionRejected", "Hedef cihaz çevrimdışı veya bulunamadı.");
        }
    }

    // 3. İsteği Kabul Etme
    public async Task AcceptConnection(string requesterDeviceId)
    {
        if (DeviceConnections.TryGetValue(requesterDeviceId, out var requesterConnectionId))
        {
            var myDeviceId = ConnectionDevices.TryGetValue(Context.ConnectionId, out var devId) ? devId : "Host";
            await Clients.Client(requesterConnectionId).SendAsync("ConnectionAccepted", myDeviceId);
        }
    }

    // 4. İsteği Reddetme
    public async Task RejectConnection(string requesterDeviceId)
    {
        if (DeviceConnections.TryGetValue(requesterDeviceId, out var requesterConnectionId))
        {
            var myDeviceId = ConnectionDevices.TryGetValue(Context.ConnectionId, out var devId) ? devId : "Host";
            await Clients.Client(requesterConnectionId).SendAsync("ConnectionRejected", myDeviceId);
        }
    }

    // 5. Ekran Karesi Aktarımı
    public async Task SendScreenFrame(string targetDeviceId, byte[] frameData)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveScreenFrame", frameData);
        }
    }

    // 6. Fare / Klavye Giriş Olayları
    public async Task SendInputEvent(string targetDeviceId, string actionType, double normX, double normY, string btn)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveInputEvent", actionType, normX, normY, btn);
        }
    }

    // 7. Oturumu İki Taraflı Kapatma
    public async Task DisconnectSession(string targetDeviceId)
    {
        if (DeviceConnections.TryGetValue(targetDeviceId, out var targetConnectionId))
        {
            await Clients.Client(targetConnectionId).SendAsync("SessionTerminated");
        }
    }

    // Bağlantı Koptuğunda Bellekten Temizleme
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionDevices.TryRemove(Context.ConnectionId, out var deviceId))
        {
            DeviceConnections.TryRemove(deviceId, out _);
        }
        return base.OnDisconnectedAsync(exception);
    }
}
