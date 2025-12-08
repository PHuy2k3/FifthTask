using System.Threading.Tasks;

namespace Store.Biz.Interfaces
{
    public interface IRealtimeNotifier
    {
        Task NotifyGroupAsync(string groupName, object payload);
    }
}
