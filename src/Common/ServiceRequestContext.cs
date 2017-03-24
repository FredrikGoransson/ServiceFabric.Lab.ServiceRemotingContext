using System;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;

namespace Common
{
    public sealed class ServiceRequestContext
    {
        private static readonly string ContextKey = Guid.NewGuid().ToString();

        public ServiceRequestContext(Guid correlationId, string user)
        {
            this.CorrelationId = correlationId;
            this.User = user;
        }

        public Guid CorrelationId { get; private set; }

        public string User { get; private set; }

        public static ServiceRequestContext Current
        {
            get { return (ServiceRequestContext)CallContext.LogicalGetData(ContextKey); }
            internal set
            {
                if (value == null)
                {
                    CallContext.FreeNamedDataSlot(ContextKey);
                }
                else
                {
                    CallContext.LogicalSetData(ContextKey, value);
                }
            }
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