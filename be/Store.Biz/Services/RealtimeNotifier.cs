using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Store.Biz.Hubs;
using Store.Biz.Interfaces;

namespace Store.Api.Services
{
    public class RealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<OrderHub> _hub;

        public RealtimeNotifier(IHubContext<OrderHub> hub)
        {
            _hub = hub;
        }

        public Task NotifyGroupAsync(string groupName, object payload)
        {
            return _hub.Clients.Group(groupName).SendAsync("OrderStatusChanged", payload);
        }
    }
}
