using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;

namespace Common
{
    public static class ServiceRemotingDispatcherExtensions
    {
        public static Task RunInRequestContext(this IServiceRemotingMessageHandler serviceRemotingDispatcher, Action action, Guid correlationId, string user)
        {
            return ServiceRequestContextHelper.RunInRequestContext(action, correlationId, user);
        }

        public static Task RunInRequestContext(this IServiceRemotingMessageHandler serviceRemotingDispatcher, Func<Task> action, Guid correlationId, string user)
        {
            return ServiceRequestContextHelper.RunInRequestContext(action, correlationId, user);
        }

        public static Task<TResult> RunInRequestContext<TResult>(this IServiceRemotingMessageHandler serviceRemotingDispatcher, Func<Task<TResult>> action, Guid correlationId, string user)
        {
            return ServiceRequestContextHelper.RunInRequestContext(action, correlationId, user);
        }
    }
}