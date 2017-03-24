using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using CalloutService;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace WebApi.Controllers
{
    public class HelloController : ApiController
    {
        public IServiceProxyFactory ServiceProxyFactory { get; set; }

        public HelloController()
        {
            ServiceProxyFactory = new ServiceProxyFactory();
        }

        // GET api/values 
        public async Task<IEnumerable<string>> Get()
        {
            var calloutServiceUri = new Uri(@"fabric:/ServiceFabric.SO.Answer._41655575/CalloutService");
            var calloutService = ServiceProxy.Create<ICalloutService>(calloutServiceUri);
            var hello = await calloutService.SayHelloAsync();

            var calloutActorServiceUri = new Uri(@"fabric:/ServiceFabric.SO.Answer._41655575/CalloutActorService");
            var calloutActor = ActorProxy.Create<ICalloutActor>(new ActorId(DateTime.Now.Millisecond), calloutActorServiceUri);

            var hello2 = await calloutActor.SayHelloAsync();

            return new string[] { hello, hello2 };
        }
    }
}