using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace BookHive.Hubs
{
    public class AnnouncementHub : Hub
    {
        public async Task SendAnnouncement(string message)
        {
            await Clients.All.SendAsync("ReceiveAnnouncement", message);
        }
    }
}