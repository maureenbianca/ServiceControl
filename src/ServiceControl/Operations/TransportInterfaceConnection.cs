namespace ServiceControl.Operations
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Extensions;

    class TransportInterfaceConnection
    {
        ManualResetEventSlim connected = new ManualResetEventSlim();

        public void SetConnected()
        {
            connected.Set();
        }

        public void SetDisconnected()
        {
            connected.Reset();
        }

        public Task WaitForConnection(CancellationToken token) => connected.WaitHandle.WaitOneAsync(token);
    }
}