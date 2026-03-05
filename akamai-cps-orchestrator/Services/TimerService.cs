using System;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Services;

public interface ITimerService
{
    void DelayBySeconds(int numberOfSeconds);
}

public class TimerService : ITimerService
{
    public void DelayBySeconds(int numberOfSeconds)
    {
        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(numberOfSeconds));
    }
}
