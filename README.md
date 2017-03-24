# ServiceFabric.Lab.ServiceRemotingContext
Setting headers for Service Remoting calls. This project demos the SO answer http://stackoverflow.com/questions/41629755/passing-user-and-auditing-information-in-calls-to-reliable-services-in-service-f/41629775?noredirect=1#comment73095783_41629775

## Question
How can I pass along auditing information between clients and services in an easy way without having to add that information as arguments for all service methods? Can I use message headers to set this data for a call?

Is there a way to allow service to pass that along downstream also, i.e., if ServiceA calls ServiceB that calls ServiceC, could the same auditing information be send to first A, then in A's call to B and then in B's call to C?


## Answer
There is actually a concept of headers that are passed between client and service if you are using [fabric transport](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-reliable-services-communication-remoting) for remoting. If you are using Http transport then you have headers there just as you would with any http request.

Now, I notice you wrote, 'is there an easier way?', and below proposal is not easy, but it solves the issue once it is in place and it is easy to use then, but if you are looking for easy in the overall code base this might not be the way to go. If that is the case then I suggest you simply add some common audit info parameter to all your service methods. The big caveat there is of course when some developer forgets to add it or it is not set properly when calling down stream services. It's all about trade-offs, as alway in code :).

**Down the rabbit hole**

In fabric transport there are two classes that are involved in the communication: an instance of a [``IServiceRemotingClient``](https://docs.microsoft.com/en-us/dotnet/api/microsoft.servicefabric.services.remoting.client.iserviceremotingclient) on the client side, and an instance of [``IServiceRemotingListener``](https://docs.microsoft.com/en-us/dotnet/api/microsoft.servicefabric.services.remoting.runtime.iserviceremotinglistener) on the service side. In each request from the client the messgae body _and_ [``ServiceRemotingMessageHeaders``](https://docs.microsoft.com/en-us/dotnet/api/microsoft.servicefabric.services.remoting.serviceremotingmessageheaders) are sent. Out of the box these headers include information of which interface (i.e. which service) and which method are being called (and that's also how the underlying receiver knows how to unpack that byte array that is the body). For calls to Actors, which goes through the ActorService, additional Actor information is also included in those headers.

The tricky part is hooking into that exchange and actually setting and then reading additional headers. Please bear with me here, it's a number of classes involved in this behind the curtains that we need to understand.

**The service side**

When you setup the ``IServiceRemotingListener`` for your service (example for a Stateless service) you usually use a convenience extension method, like so:

<!-- language-all: lang-cs -->

     protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
     {
         yield return new ServiceInstanceListener(context => 
             this.CreateServiceRemotingListener(this.Context));
     }

_(Another way to do it would be to implement your own listener, but that's not really what we wan't to do here, we just wan't to add things on top of the existing infrastructure. See below for that approach.)_

This is where we can provide our own listener instead, similar to what that extention method does behind the curtains. Let's first look at what that extention method does. It goes looking for a specific attribute on assembly level on your service project: [``ServiceRemotingProviderAttribute``](https://docs.microsoft.com/en-us/dotnet/api/microsoft.servicefabric.services.remoting.serviceremotingproviderattribute). That one is ``abstract``, but the one that you can use, and which you will get a default instance of, if none is provided, is [``FabricTransportServiceRemotingProviderAttribute``](https://docs.microsoft.com/en-us/dotnet/api/microsoft.servicefabric.services.remoting.fabrictransport.fabrictransportserviceremotingproviderattribute). Set it in ``AssemblyInfo.cs`` (or any other file, it's an assembly attribute):

    [assembly: FabricTransportServiceRemotingProvider()]

 This attribute has two interesting overridable methods:

    public override IServiceRemotingListener CreateServiceRemotingListener(
        ServiceContext serviceContext, IService serviceImplementation)
    public override IServiceRemotingClientFactory CreateServiceRemotingClientFactory(
        IServiceRemotingCallbackClient callbackClient)

These two methods are responsible for creating the the listener and the client factory. That means that it is also inspected by the client side of the transaction. That is why it is an attribute on assembly level for the service assembly, the client side can also pick it up together with the ``IService`` interface for the client we want to communicate with.

The ``CreateServiceRemotingListener`` ends up creating a ``FabricTransportServiceRemotingListener``, however one where we cannot set our own specific [``IServiceRemotingMessageHandler``](https://docs.microsoft.com/en-us/dotnet/api/microsoft.servicefabric.services.remoting.runtime.iserviceremotingmessagehandler). If you create your own sub class of ``FabricTransportServiceRemotingProviderAttribute`` and override that then you can actually make it create an instance of ``FabricTransportServiceRemotingListener`` that takes in a dispatcher in the constructor:

    public class AuditableFabricTransportServiceRemotingProviderAttribute : 
        FabricTransportServiceRemotingProviderAttribute
    {
        public override IServiceRemotingListener CreateServiceRemotingListener(ServiceContext serviceContext, IService serviceImplementation)
        {
                var messageHandler = new AuditableServiceRemotingDispatcher(serviceContext, serviceImplementation);

                return (IServiceRemotingListener)new FabricTransportServiceRemotingListener(
                    serviceContext: serviceContext,
                    messageHandler: messageHandler);
        }
    }

The ``AuditableServiceRemotingDispatcher`` is where the magic happens. It is our own ``ServiceRemotingDispatcher`` subclass. Override the ``RequestResponseAsync`` (ignore  ``HandleOneWay``, it is not supported by service remoting, it throws an ``NotImplementedException`` if called), like this:

    public override async Task<byte[]> RequestResponseAsync(IServiceRemotingRequestContext requestContext, ServiceRemotingMessageHeaders messageHeaders, byte[] requestBodyBytes)
    {
        byte[] userHeader = null;
        if (messageHeaders.TryGetHeaderValue("user-header", out auditHeader))
        {
            // Deserialize from byte[] and handle the header
        }
        else
        {
            // Throw exception?
        }

        byte[] result = null;        
        result = await base.RequestResponseAsync(requestContext, messageHeaders, requestBodyBytes);
        return result;
    }

Another, easier, but less flexible way, would be to directly create an instance of ``FabricTransportServiceRemotingListener`` with an instance of our custom dispatcher directly in the service:

     protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
     {
         yield return new ServiceInstanceListener(context => 
             new FabricTransportServiceRemotingListener(this.Context, new AuditableServiceRemotingDispatcher(context, this)));
     }

_Why is this less flexible? Well, because using the attribute supports the client side as well, as we see below_

**The client side**

Ok, so now we can read custom headers when receiving messages, how about setting those? Let's look at the other method of that attribute:

    public override IServiceRemotingClientFactory CreateServiceRemotingClientFactory(IServiceRemotingCallbackClient callbackClient)
    {
        var fabricTransportSettings = GetDefaultFabricTransportSettings();
        fabricTransportSettings.MaxMessageSize = this.GetAndValidateMaxMessageSize(fabricTransportSettings.MaxMessageSize);
        fabricTransportSettings.OperationTimeout = this.GetAndValidateOperationTimeout(fabricTransportSettings.OperationTimeout);
        fabricTransportSettings.KeepAliveTimeout = this.GetKeepAliveTimeout(fabricTransportSettings.KeepAliveTimeout);
        return (IServiceRemotingClientFactory)new FabricTransportServiceRemotingClientFactory(
            fabricTransportSettings: fabricTransportSettings,
            callbackClient: callbackClient,
            servicePartitionResolver: (IServicePartitionResolver)null,
            exceptionHandlers:  (IEnumerable<IExceptionHandler>) null,
            traceId: (string)null);
    }

Here we cannot just inject a specific handler or similar as for the service, we have to supply our own custom factory. In order not to have to reimplement the particulars of ``FabricTransportServiceRemotingClientFactory`` I simply encapsulate it in my own implementation of ``IServiceRemotingClientFactory`` (yes, you need all off that):

    public class AuditedFabricTransportServiceRemotingClientFactory : IServiceRemotingClientFactory, ICommunicationClientFactory<IServiceRemotingClient>
    {
        private readonly ICommunicationClientFactory<IServiceRemotingClient> _innerClientFactory;
        public AuditedFabricTransportServiceRemotingClientFactory(ICommunicationClientFactory<IServiceRemotingClient> innerClientFactory)
        {
            _innerClientFactory = innerClientFactory;
            _innerClientFactory.ClientConnected += OnClientConnected;
            _innerClientFactory.ClientDisconnected += OnClientDisconnected;
        }

        private void OnClientConnected(object sender, CommunicationClientEventArgs<IServiceRemotingClient> e)
        {
            EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> clientConnected = this.ClientConnected;
            if (clientConnected == null) return;
            clientConnected((object)this, new CommunicationClientEventArgs<IServiceRemotingClient>()
            {
                Client = e.Client
            });
        }

        private void OnClientDisconnected(object sender, CommunicationClientEventArgs<IServiceRemotingClient> e)
        {
            EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> clientDisconnected = this.ClientDisconnected;
            if (clientDisconnected == null) return;
            clientDisconnected((object)this, new CommunicationClientEventArgs<IServiceRemotingClient>()
            {
                Client = e.Client
            });
        }

        public async Task<IServiceRemotingClient> GetClientAsync(Uri serviceUri, ServicePartitionKey partitionKey, TargetReplicaSelector targetReplicaSelector, string listenerName,
            OperationRetrySettings retrySettings, CancellationToken cancellationToken)
        {
            var client = await _innerClientFactory.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, cancellationToken);
            return new AuditedFabricTransportServiceRemotingClient(client);
        }

        public async Task<IServiceRemotingClient> GetClientAsync(ResolvedServicePartition previousRsp, TargetReplicaSelector targetReplicaSelector, string listenerName, OperationRetrySettings retrySettings,
            CancellationToken cancellationToken)
        {
            var client = await _innerClientFactory.GetClientAsync(previousRsp, targetReplicaSelector, listenerName, retrySettings, cancellationToken);
            return new AuditedFabricTransportServiceRemotingClient(client);
        }

        public Task<OperationRetryControl> ReportOperationExceptionAsync(IServiceRemotingClient client, ExceptionInformation exceptionInformation, OperationRetrySettings retrySettings,
            CancellationToken cancellationToken)
        {
            return _innerClientFactory.ReportOperationExceptionAsync(client, exceptionInformation, retrySettings, cancellationToken);
        }

        public event EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> ClientConnected;
        public event EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> ClientDisconnected;
    }

This implementation simply passes along anything heavy lifting to the underlying factory, while returning it's own auditable client that similarily encapsulates a ``IServiceRemotingClient``:

     public class AuditedFabricTransportServiceRemotingClient : IServiceRemotingClient, ICommunicationClient
    {
        private readonly IServiceRemotingClient _innerClient;

        public AuditedFabricTransportServiceRemotingClient(IServiceRemotingClient innerClient)
        {
            _innerClient = innerClient;
        }

        ~AuditedFabricTransportServiceRemotingClient()
        {
            if (this._innerClient == null) return;
            var disposable = this._innerClient as IDisposable;
            disposable?.Dispose();
        }

        Task<byte[]> IServiceRemotingClient.RequestResponseAsync(ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {            
            messageHeaders.SetUser(ServiceRequestContext.Current.User);
            messageHeaders.SetCorrelationId(ServiceRequestContext.Current.CorrelationId);
            return this._innerClient.RequestResponseAsync(messageHeaders, requestBody);
        }

        void IServiceRemotingClient.SendOneWay(ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            messageHeaders.SetUser(ServiceRequestContext.Current.User);
            messageHeaders.SetCorrelationId(ServiceRequestContext.Current.CorrelationId);
            this._innerClient.SendOneWay(messageHeaders, requestBody);
        }

        public ResolvedServicePartition ResolvedServicePartition
        {
            get { return this._innerClient.ResolvedServicePartition; }
            set { this._innerClient.ResolvedServicePartition = value; }
        }

        public string ListenerName
        {
            get { return this._innerClient.ListenerName; }
            set { this._innerClient.ListenerName = value; }
        }
        public ResolvedServiceEndpoint Endpoint
        {
            get { return this._innerClient.Endpoint; }
            set { this._innerClient.Endpoint = value; }
        }
    }

Now, in here is where we actually (and finally) set the audit name that we want to pass along to the service.

One final piece of the puzzle, the ServiceRequestContext, which is a custom class that allows us to handle an ambient context for a service request call. This is relevant because it gives us an easy way to propagate that context information, like the user or a correlation id, in a chain of calls. The implementation ``ServiceRequestContext`` looks like:

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

This last part was much influenced by the [SO answer](http://stackoverflow.com/a/6701106/1062217) by Stephen Cleary. It gives us an easy way to handle the ambient information down a hierarcy of calls, weather they are synchronous or asyncronous over Tasks. Now, with this we have a way of setting that information also in the Dispatcher on the service side:

        public override Task<byte[]> RequestResponseAsync(IServiceRemotingRequestContext requestContext, ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            var user = messageHeaders.GetUser();
            var correlationId = messageHeaders.GetCorrelationId();

            return ServiceRequestContext.RunInRequestContext(async () => await base.RequestResponseAsync(requestContext, messageHeaders, requestBody), correlationId, user);
        }

(``GetUser()`` and ``GetCorrelationId()`` are just helper methods that gets and unpacks the headers set by the client)

Having this in place means that any new client created by the service for any aditional call will also have the sam headers set, so in the scenario ServiceA -> ServiceB -> ServiceC we will still have the same user set in the call from ServiceB to ServiceC.

_what? that easy? yes :)_

From inside a service, for instance a Stateless OWIN web api, where you first capture the user information, you create an instance of ``ServiceProxyFactory``and wrap that call in a ``ServiceRequestContext``:

    var task = ServiceRequestContext.RunInRequestContext(async () =>
    {
        var serviceA = ServiceProxyFactory.CreateServiceProxy<IServiceA>(new Uri($"{FabricRuntime.GetActivationContext().ApplicationName}/ServiceA"));
        await serviceA.DoStuffAsync(CancellationToken.None);
    }, Guid.NewGuid(), user);

Ok, so to sum it up - you can hook into the service remoting to set your own headers. As we see above there is some work that needs to be done to get that to work, mainly creating your own subclasses of the underlying infrastructure. The upside is that once you have this in place, then you have a mechanism for auditing your service calls 
