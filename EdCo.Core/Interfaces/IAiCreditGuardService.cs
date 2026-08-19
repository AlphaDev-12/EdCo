using System.Threading.Tasks;

namespace EdCo.Core.Interfaces
{
    public interface IAiCreditGuardService
    {
        Task<(bool Allowed, string? ErrorMessage)> ReserveHoldingCreditAsync(string userId, decimal estimatedCost);
        Task ReleaseHoldingCreditAsync(string userId, decimal estimatedCost);
    }
}
