using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Common
{
    internal static class ServiceRequestContextHelper
    {
        public static Task RunInRequestContext(Action action, Guid correlationId, string user)
        {
            Task task = null;

            task = new Task(() =>
            {
                Debug.Assert(ServiceRequestContext.Current == null);
                ServiceRequestContext.Current = new ServiceRequestContext(correlationId, user);
                try
                {
                    action();
                }
                finally
                {
                    ServiceRequestContext.Current = null;
                }
            });

            task.Start();

            return task;
        }

        public static Task RunInRequestContext(Func<Task> action, Guid correlationId, string user)
        {
            Task<Task> task = null;

            task = new Task<Task>(async () =>
            {
                Debug.Assert(ServiceRequestContext.Current == null);
                ServiceRequestContext.Current = new ServiceRequestContext(correlationId, user);
                try
                {
                    await action();
                }
                finally
                {
                    ServiceRequestContext.Current = null;
                }
            });

            task.Start();

            return task.Unwrap();
        }

        public static Task<TResult> RunInRequestContext<TResult>(Func<Task<TResult>> action, Guid correlationId, string user)
        {
            Task<Task<TResult>> task = null;

            task = new Task<Task<TResult>>(async () =>
            {
                Debug.Assert(ServiceRequestContext.Current == null);
                ServiceRequestContext.Current = new ServiceRequestContext(correlationId, user);
                try
                {
                    return await action();
                }
                finally
                {
                    ServiceRequestContext.Current = null;
                }

            });

            task.Start();

            return task.Unwrap<TResult>();
        }
    }
}