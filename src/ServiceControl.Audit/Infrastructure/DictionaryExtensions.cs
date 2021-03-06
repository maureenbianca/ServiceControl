namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Collections.Generic;

    static class DictionaryExtensions
    {
        public static void CheckIfKeyExists(string key, IReadOnlyDictionary<string, string> headers, Action<string> actionToInvokeWhenKeyIsFound)
        {
            if (headers.TryGetValue(key, out var value))
            {
                actionToInvokeWhenKeyIsFound(value);
            }
        }
    }
}