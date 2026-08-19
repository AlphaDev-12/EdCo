using System;
using System.Linq;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EdCo.Core.Services
{
    public class AiCreditGuardService : IAiCreditGuardService
    {
        private readonly EdCoDbContext _context;
        private readonly ICacheService _cacheService;
        private readonly ILogger<AiCreditGuardService> _logger;
        private const decimal MonthlyLimit = 0.50m;

        public AiCreditGuardService(
            EdCoDbContext context,
            ICacheService cacheService,
            ILogger<AiCreditGuardService> logger)
        {
            _context = context;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<(bool Allowed, string? ErrorMessage)> ReserveHoldingCreditAsync(string userId, decimal estimatedCost)
        {
            if (string.IsNullOrEmpty(userId))
            {
                // Anonymous user or untracked credit
                return (true, null);
            }

            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;

            // Fetch accumulated cost logged in DB for this month
            var totalCostThisMonth = await _context.AiInteractionLogs
                .Where(l => l.AppUserId == userId && l.Timestamp.Month == currentMonth && l.Timestamp.Year == currentYear)
                .SumAsync(l => (decimal?)l.Cost) ?? 0m;

            // Fetch active holdings from distributed cache
            var holdingsKey = $"credit_holding:{userId}:{currentYear}:{currentMonth}";
            var currentHoldings = await _cacheService.GetAsync<decimal>(holdingsKey);

            if ((totalCostThisMonth + currentHoldings + estimatedCost) > MonthlyLimit)
            {
                _logger.LogWarning("User {UserId} exceeded monthly AI credit limit ($0.50). DB Cost: {DbCost}, Holdings: {Holdings}, Requested: {EstCost}",
                    userId, totalCostThisMonth, currentHoldings, estimatedCost);
                return (false, "Monthly AI usage limit reached ($0.50).");
            }

            // Reserve holding credit
            var newHoldings = currentHoldings + estimatedCost;
            await _cacheService.SetAsync(holdingsKey, newHoldings, TimeSpan.FromHours(1));

            return (true, null);
        }

        public async Task ReleaseHoldingCreditAsync(string userId, decimal estimatedCost)
        {
            if (string.IsNullOrEmpty(userId)) return;

            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;
            var holdingsKey = $"credit_holding:{userId}:{currentYear}:{currentMonth}";

            var currentHoldings = await _cacheService.GetAsync<decimal>(holdingsKey);
            var updated = Math.Max(0m, currentHoldings - estimatedCost);
            await _cacheService.SetAsync(holdingsKey, updated, TimeSpan.FromHours(1));
        }
    }
}
