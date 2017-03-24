using System;
using System.Collections.Generic;
using System.Fabric;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;

namespace Common
{
    public class AuditableServiceRemotingDispatcher : ServiceRemotingDispatcher
    {
        public AuditableServiceRemotingDispatcher(ServiceContext serviceContext, IService service) :
            base(serviceContext, service)
        {
        }

        public override Task<byte[]> RequestResponseAsync(
            IServiceRemotingRequestContext requestContext,
            ServiceRemotingMessageHeaders messageHeaders,
            byte[] requestBody)
        {
            var user = messageHeaders.GetUser();
            var correlationId = messageHeaders.GetCorrelationId();

            var headersInternal = messageHeaders.GetPrivateField< ServiceRemotingMessageHeaders, Dictionary <string, byte[]>>("headers");

            var headerNames = headersInternal.Keys;


            return ServiceRequestContext.RunInRequestContext(
                async () => await base.RequestResponseAsync(
                    requestContext,
                    messageHeaders,
                    requestBody),
                correlationId, user);
        }
    }
}