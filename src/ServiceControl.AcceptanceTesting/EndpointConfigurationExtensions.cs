﻿namespace ServiceControl.AcceptanceTesting
{
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;

    public static class EndpointConfigurationExtensions
    {
        public static void NoImmediateRetries(this EndpointConfiguration configuration)
        {
            configuration.Recoverability().Immediate(x => x.NumberOfRetries(0));
        }

        public static void NoDelayedRetries(this EndpointConfiguration configuration)
        {
            configuration.Recoverability().Delayed(x => x.NumberOfRetries(0));
        }

        public static void NoRetries(this EndpointConfiguration configuration)
        {
            configuration.NoDelayedRetries();
            configuration.NoImmediateRetries();
        }

        public static void NoOutbox(this EndpointConfiguration configuration)
        {
            configuration.DisableFeature<Outbox>();
        }

        public static TransportExtensions ConfigureTransport(this EndpointConfiguration endpointConfiguration)
        {
            return new TransportExtensions(endpointConfiguration.GetSettings());
        }
    }
}