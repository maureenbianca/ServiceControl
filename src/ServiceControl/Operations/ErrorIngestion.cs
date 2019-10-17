namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestion
    {
        public ErrorIngestion(ErrorIngestor errorIngestor, Settings settings, RawEndpointFactory rawEndpointFactory, SatelliteImportFailuresHandler importFailuresHandler)
        {
            this.errorIngestor = errorIngestor;
            this.settings = settings;
            this.rawEndpointFactory = rawEndpointFactory;
            this.importFailuresHandler = importFailuresHandler;
        }

        public async Task Start()
        {
            if (IsRunning)
            {
                return;
            }

            var rawConfiguration = rawEndpointFactory.CreateRawEndpointConfiguration(
                settings.ErrorQueue,
                (messageContext, dispatcher) => errorIngestor.Ingest(messageContext, dispatcher),
                null);

            rawConfiguration.CustomErrorHandlingPolicy(new ErrorIngestionFaultPolicy(importFailuresHandler));

            var startableRaw = await RawEndpoint.Create(rawConfiguration).ConfigureAwait(false);

            if (settings.ForwardErrorMessages)
            {
                await errorIngestor.VerifyCanReachForwardingAddress(settings.ErrorLogQueue, startableRaw).ConfigureAwait(false);
            }

            ingestionEndpoint = await RawEndpoint.Start(rawConfiguration)
                .ConfigureAwait(false);
        }

        public async Task Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            await ingestionEndpoint.Stop()
                .ConfigureAwait(false);
            ingestionEndpoint = null;
        }

        public bool IsRunning => ingestionEndpoint != null;

        ErrorIngestor errorIngestor;
        Settings settings;
        RawEndpointFactory rawEndpointFactory;
        SatelliteImportFailuresHandler importFailuresHandler;

        IReceivingRawEndpoint ingestionEndpoint;
    }
}