using Microsoft.Extensions.DependencyInjection;
using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace akamai_cps_orchestrator.Tests.Jobs;

public abstract class BaseJobTest<T> where T : class
{
    protected readonly ILogger Logger;
    
    protected BaseJobTest(ITestOutputHelper output)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(output, new XUnitLoggerOptions()));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var provider = serviceCollection.BuildServiceProvider();
        Logger = provider.GetRequiredService<ILogger<T>>();
    }
}
