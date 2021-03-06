﻿namespace ServiceControl.Audit.Auditing
{
    using System.Collections.Generic;
    using NServiceBus;

    class AuditEnricherContext
    {
        public AuditEnricherContext(IReadOnlyDictionary<string, string> headers, IList<ICommand> outgoingCommands, IDictionary<string, object> metadata)
        {
            Headers = headers;
            this.outgoingCommands = outgoingCommands;
            Metadata = metadata;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IDictionary<string, object> Metadata { get; }

        public void AddForSend(ICommand command)
        {
            outgoingCommands.Add(command);
        }

        IList<ICommand> outgoingCommands;
    }
}