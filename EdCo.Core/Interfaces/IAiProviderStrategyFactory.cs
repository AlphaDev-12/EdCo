namespace EdCo.Core.Interfaces
{
    public interface IAiProviderStrategyFactory
    {
        IAiProviderStrategy GetStrategy(string provider);
    }
}
