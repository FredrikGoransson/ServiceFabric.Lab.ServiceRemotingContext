using System;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace Common
{
    public static class ServiceRemotingClientExtensions
    {
        public static Task RunInRequestContext(this IServiceRemotingClient serviceRemotingClient, Action action, Guid correlationId, string user)
        {
            return ServiceRequestContextHelper.RunInRequestContext(action, correlationId, user);
        }

        public static Task RunInRequestContext(this IServiceRemotingClient serviceRemotingClient, Func<Task> action, Guid correlationId, string user)
        {
            return ServiceRequestContextHelper.RunInRequestContext(action, correlationId, user);
        }

        public static Task<TResult> RunInRequestContext<TResult>(this IServiceRemotingClient serviceRemotingClient, Func<Task<TResult>> action, Guid correlationId, string user)
        {
            return ServiceRequestContextHelper.RunInRequestContext(action, correlationId, user);
        }
    }
}