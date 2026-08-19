using System;
using System.Collections.Generic;
using System.Linq;
using EdCo.Core.Interfaces;

namespace EdCo.Core.Services.Providers
{
    public class AiProviderStrategyFactory : IAiProviderStrategyFactory
    {
        private readonly IEnumerable<IAiProviderStrategy> _strategies;

        public AiProviderStrategyFactory(IEnumerable<IAiProviderStrategy> strategies)
        {
            _strategies = strategies;
        }

        public IAiProviderStrategy GetStrategy(string provider)
        {
            var target = string.IsNullOrWhiteSpace(provider) ? "Groq" : provider.Trim();
            var strategy = _strategies.FirstOrDefault(s => string.Equals(s.ProviderName, target, StringComparison.OrdinalIgnoreCase));
            return strategy ?? _strategies.First(s => string.Equals(s.ProviderName, "Groq", StringComparison.OrdinalIgnoreCase));
        }
    }
}
