namespace HiddenSwitch.Multiplayer.Tests
{
    using System;

    public interface ITimer
    {
        bool IsStarted();

        void StartTimer(Action action, long interval);
               
        void StopTimer();
    }
}