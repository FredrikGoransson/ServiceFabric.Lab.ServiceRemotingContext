using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace SecondService
{
    public interface ISecondService : IService
    {
        Task DoStuffAsync(string unique, CancellationToken cancellationToken);
    }
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class SecondService : StatelessService, ISecondService
    {
        public IServiceProxyFactory ServiceProxyFactory { get; set; }

        public SecondService(StatelessServiceContext context)
            : base(context)
        {
            ServiceProxyFactory = new ServiceProxyFactory();            
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            yield return new ServiceInstanceListener(context => this.CreateServiceRemotingListener(this.Context));
        }        

        public async Task DoStuffAsync(string unique, CancellationToken cancellationToken)
        {
            ServiceEventSource.Current.ServiceMessage(this.Context, $"{nameof(SecondService)} called by {ServiceRequestContext.Current.User} with {unique} in {ServiceRequestContext.Current.CorrelationId}");
            await Task.Delay(300, cancellationToken);
        }
    }
}
