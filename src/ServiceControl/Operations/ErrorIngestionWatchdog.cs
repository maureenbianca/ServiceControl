namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus.Logging;

    class ErrorIngestionWatchdog
    {
        ErrorIngestion errorIngestion;
        CancellationTokenSource shuttingDown;
        TimeSpan timeToWaitForRestart = TimeSpan.FromSeconds(60);

        public ErrorIngestionWatchdog(ShutdownNotifier shutdownNotifier)
        {
            shuttingDown = new CancellationTokenSource();
            shutdownNotifier.Register(() => shuttingDown.Cancel(false));
        }

        public void Watch(ErrorIngestion target)
        {
            errorIngestion = target;
        }

        public async Task Trigger(string message)
        {
            if (errorIngestion == null || !errorIngestion.IsRunning)
            {
                return;
            }

            Log.Warn($"{message}. Shutting down error ingestion for 60 seconds.");
            // TODO: Raise an event

            await errorIngestion.Stop()
                .ConfigureAwait(false);

            await Task.Delay(timeToWaitForRestart, shuttingDown.Token)
                .ConfigureAwait(false);

            if (!shuttingDown.IsCancellationRequested)
            {
                Log.Info("Restarting error ingestion.");

                await errorIngestion.Start()
                    .ConfigureAwait(false);
            }
        }

        static ILog Log = LogManager.GetLogger<ErrorIngestionWatchdog>();
    }
}