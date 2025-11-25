using Microsoft.AspNetCore.SignalR;

namespace RestaurantManagement.API.Hubs;

public class OrderHub : Hub
{
    public async Task JoinGroup(string waiterId)
    {
        if (string.IsNullOrWhiteSpace(waiterId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, waiterId);
    }
        public async Task LeaveGroup(string waiterId)
        {
            if (string.IsNullOrWhiteSpace(waiterId)) return;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, waiterId);
        }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // It does nothing on disconnect for now
        await base.OnDisconnectedAsync(exception);
    }
}
