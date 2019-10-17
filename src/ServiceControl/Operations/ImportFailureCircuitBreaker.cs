namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using NServiceBus;

    class ImportFailureCircuitBreaker : IDisposable
    {
        public ImportFailureCircuitBreaker(CriticalError criticalError, ErrorIngestionWatchdog watchdog)
        {
            this.criticalError = criticalError;
            this.watchdog = watchdog;
            timer = new Timer(_ => FlushHistory(), null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(20));
        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        void FlushHistory()
        {
            Interlocked.Exchange(ref failureCount, 0);
        }

        public void Increment(Exception lastException)
        {
            var result = Interlocked.Increment(ref failureCount);
            if (result > 50)
            {
                criticalError.Raise("Failed to import too many times", lastException);
                watchdog.Trigger("Failed to import too many times").GetAwaiter().GetResult();
            }
        }

        readonly CriticalError criticalError;
        readonly ErrorIngestionWatchdog watchdog;
        Timer timer;
        long failureCount;
    }
}