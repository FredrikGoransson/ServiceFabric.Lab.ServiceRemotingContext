using System;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace Common
{
    public static class ServiceProxyFactoryExtensions
    {
        public static Task RunInRequestContext(this IServiceProxyFactory serviceProxyFactory, Action action, Guid correlationId, string user)
        {
            return ServiceRequestContextHelper.RunInRequestContext(action, correlationId, user);
        }

        public static Task RunInRequestContext(this IServiceProxyFactory serviceProxyFactory, Func<Task> action, Guid correlationId, string user)
        {
            return ServiceRequestContextHelper.RunInRequestContext(action, correlationId, user);
        }

        public static Task<TResult> RunInRequestContext<TResult>(this IServiceProxyFactory serviceProxyFactory, Func<Task<TResult>> action, Guid correlationId, string user)
        {
            return ServiceRequestContextHelper.RunInRequestContext(action, correlationId, user);
        }
    }
}