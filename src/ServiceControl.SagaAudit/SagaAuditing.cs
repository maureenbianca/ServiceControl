﻿namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;

    public static class InvokedSagasParser
    {
        public static void Parse(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
        {
            if (headers.TryGetValue("NServiceBus.InvokedSagas", out var sagasInvokedRaw))
            {
                var sagasChanges = new Dictionary<string, string>();
                if (headers.TryGetValue("ServiceControl.SagaStateChange", out var sagasChangeRaw))
                {
                    var multiSagaChanges = sagasChangeRaw.Split(';');

                    foreach (var part in multiSagaChanges.Select(s => s.Split(':')))
                    {
                        var id = part[0];
                        var thisChange = part[1];
                        if (!sagasChanges.TryGetValue(id, out var previousChange))
                        {
                            sagasChanges[id] = thisChange;
                        }
                        else
                        {
                            if (thisChange == "Completed" //Completed overrides everything
                                || thisChange == "New" && previousChange == "Updated") //New overrides Updated
                            {
                                sagasChanges[id] = thisChange;
                            }
                        }
                    }
                }

                var invokedSagas = SplitInvokedSagas(sagasInvokedRaw);

                var sagas = invokedSagas
                    .Distinct()
                    .Select(saga =>
                    {
                        var sagaInvoked = saga.Split(':');

                        sagasChanges.TryGetValue(sagaInvoked[1], out var changeText);

                        return new SagaInfo
                        {
                            SagaId = Guid.Parse(sagaInvoked[1]),
                            SagaType = sagaInvoked[0],
                            ChangeStatus = changeText
                        };
                    })
                    .ToList();

                metadata.Add("InvokedSagas", sagas);
            }
            else
            {
                //for backwards compatibility
                if (headers.TryGetValue(Headers.SagaId, out var sagaId))
                {
                    // A failure when a MarkAsComplete control message is received causes a saga message to be received in
                    // the error queue without a Headers.SagaType header.
                    // Hence the reason for the check!
                    if (headers.TryGetValue(Headers.SagaType, out var sagaType))
                    {
                        sagaType = sagaType.Split(',').First();
                    }
                    else
                    {
                        sagaType = "Unknown";
                    }

                    metadata.Add("InvokedSagas", new List<SagaInfo>
                        {
                            new SagaInfo
                            {
                                SagaId = Guid.Parse(sagaId),
                                SagaType = sagaType
                            }
                        });
                }
            }

            if (headers.TryGetValue(Headers.OriginatingSagaId, out var originatingSagaId))
            {
                // I am not sure if we need this logic here as well, but just in case see comment above.
                if (headers.TryGetValue(Headers.OriginatingSagaType, out var sagaType))
                {
                    sagaType = sagaType.Split(',').First();
                }
                else
                {
                    sagaType = "Unknown";
                }

                metadata.Add("OriginatesFromSaga", new SagaInfo
                {
                    SagaId = Guid.Parse(originatingSagaId),
                    SagaType = sagaType
                });
            }
        }

        static IEnumerable<string> SplitInvokedSagas(string sagasInvokedRaw)
        {
            var semicolonCount = sagasInvokedRaw.Count(c => c == ';');
            var colonCount = sagasInvokedRaw.Count(c => c == ':');
            if (colonCount != semicolonCount + 1) //Malformed data coming from old version of saga audit plugin
            {
                var tailSemicolon = sagasInvokedRaw.LastIndexOf(";", StringComparison.Ordinal);
                var tail = sagasInvokedRaw.Substring(tailSemicolon + 1);
                var head = sagasInvokedRaw.Substring(0, tailSemicolon);

                var headDeduplicated = head.Substring(0, head.Length / 2);

                foreach (var part in SplitInvokedSagas(headDeduplicated))
                {
                    yield return part;
                }

                yield return tail;
            }
            else
            {
                foreach (var part in sagasInvokedRaw.Split(';'))
                {
                    yield return part;
                }
            }
        }
    }
}