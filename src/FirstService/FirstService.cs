using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using SecondService;

namespace FirstService
{
    internal sealed class FirstService : StatelessService, IFirstService
    {
        public IServiceProxyFactory ServiceProxyFactory { get; set; }

        public FirstService(StatelessServiceContext context)
            : base(context)
        {
            ServiceProxyFactory = new ServiceProxyFactory();

        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            //yield return new ServiceInstanceListener(context => this.CreateServiceRemotingListener(this.Context));
            yield return new ServiceInstanceListener(context => 
            new Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime.FabricTransportServiceRemotingListener(
                this.Context, new AuditableServiceRemotingDispatcher(context, this)));
        }

        public async Task DoStuffAsync(string unique, CancellationToken cancellationToken)
        {
            ServiceEventSource.Current.ServiceMessage(this.Context, $"{nameof(FirstService)} called by {ServiceRequestContext.Current.User} with {unique} in {ServiceRequestContext.Current.CorrelationId}");
            await Task.Delay(300, cancellationToken);

            var secondService = ServiceProxyFactory.CreateServiceProxy<ISecondService>(new Uri($"{this.Context.CodePackageActivationContext.ApplicationName}/SecondService"));
            await secondService.DoStuffAsync(unique, cancellationToken);
        }
    }


}
