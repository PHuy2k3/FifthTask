using Store.Biz.Services;
using System.Threading.Tasks;

namespace Store.Biz.Interfaces
{
    public interface IOrderService
    {
        Task<Result> UpdateOrderStatusAsync(long orderId, string newStatus, string changedByUserId);
    }
}
