using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MURO.API.Hubs;

/// <summary>
/// Admin'lere özel WebSocket hub — real-time dashboard güncellemeleri.
/// 
/// Channels:
/// - "DashboardUpdate" → istatistik güncellemesi (öğrenci sayısı, aktif ders vb.)
/// - "LiveSessionUpdate" → canlı ders başladı/bitti
/// - "OnlineCountUpdate" → anlık online kullanıcı sayısı
/// </summary>
[Authorize]
public class AdminHub : Hub
{
    private static int _connectedAdmins;

    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        
        // Sadece Admin rolündeki kullanıcılar bu hub'a bağlanabilir
        if (role != "Admin")
        {
            Context.Abort();
            return;
        }

        Interlocked.Increment(ref _connectedAdmins);
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

        // Bağlanan admin'e mevcut online sayısını gönder
        await Clients.Caller.SendAsync("OnlineCountUpdate", new { count = _connectedAdmins });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _connectedAdmins);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");
        
        // Kalan admin'lere güncelle
        await Clients.Group("admins").SendAsync("OnlineCountUpdate", new { count = _connectedAdmins });

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Tüm admin'lere dashboard istatistik güncellemesi push eder.
    /// Herhangi bir service'ten çağrılabilir: IHubContext&lt;AdminHub&gt; inject et.
    /// </summary>
    public static async Task BroadcastDashboardUpdate(IHubContext<AdminHub> hubContext, object stats)
    {
        await hubContext.Clients.Group("admins").SendAsync("DashboardUpdate", stats);
    }

    /// <summary>
    /// Canlı ders durumu değiştiğinde (başlama/bitiş) admin'lere bildir.
    /// </summary>
    public static async Task BroadcastLiveSessionUpdate(IHubContext<AdminHub> hubContext, object sessionInfo)
    {
        await hubContext.Clients.Group("admins").SendAsync("LiveSessionUpdate", sessionInfo);
    }
}
