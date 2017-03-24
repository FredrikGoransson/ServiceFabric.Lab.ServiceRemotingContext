using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.FabricTransport.Common;
using Microsoft.ServiceFabric.Services.Communication.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;

namespace Common
{
    public class AuditableFabricTransportServiceRemotingProviderAttribute : FabricTransportServiceRemotingProviderAttribute
    {
        public AuditableFabricTransportServiceRemotingProviderAttribute(params Type[] exceptionHandlerTypes)
        {
            ExceptionHandlerTypes = exceptionHandlerTypes;
        }

        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public IEnumerable<Type> ExceptionHandlerTypes { get; set; }

        /// <summary>
        ///     Creates a service remoting listener for remoting the service interface.
        ///     Uses the <see cref="T:FFCG.FFCG.ServiceFabric.Services.Remoting.Runtime.ServiceRemotingDispatcher"/> to dispatch
        ///     service method invocations.
        ///     Exception Handlers <see cref="T:Microsoft.ServiceFabric.Services.Communication.Client.IExceptionHandler"/> can be attached by
        ///     adding them to the the constructor of this attribute
        /// </summary>
        /// <param name="serviceContext">
        ///     The context of the service for which the remoting listener is being constructed.
        /// </param>
        /// <param name="serviceImplementation">
        ///     The service implementation object.
        /// </param>
        /// <returns>
        ///     A <see cref="T:Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime.FabricTransportServiceRemotingListener" />
        ///     as <see cref="T:Microsoft.ServiceFabric.Services.Remoting.Runtime.IServiceRemotingListener" />
        ///     for the specified service implementation.
        /// </returns>
        public override IServiceRemotingListener CreateServiceRemotingListener(ServiceContext serviceContext, IService serviceImplementation)
        {
            var messageHandler = new AuditableServiceRemotingDispatcher(serviceContext, serviceImplementation);

            return (IServiceRemotingListener)new FabricTransportServiceRemotingListener(
                serviceContext: serviceContext,
                messageHandler: messageHandler);
        }

        /// <summary>
        ///     Creates a service remoting client factory for connecting to the service over remoted service interfaces.
        /// </summary>
        /// <param name="callbackClient">
        ///     Client implementation where the callbacks should be dispatched.
        /// </param>
        /// <returns>
        ///     A <see cref="T:Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Client.FabricTransportServiceRemotingClientFactory" />
        ///     as <see cref="T:Microsoft.ServiceFabric.Services.Remoting.Client.IServiceRemotingClientFactory" />
        ///     that can be used with <see cref="T:Microsoft.ServiceFabric.Services.Remoting.Client.ServiceProxyFactory" /> to
        ///     generate service proxy to talk to a stateless or stateful service over remoted actor interface.
        /// </returns>
        public override IServiceRemotingClientFactory CreateServiceRemotingClientFactory(IServiceRemotingCallbackClient callbackClient)
        {
            var fabricTransportSettings = GetDefaultFabricTransportSettings();
            fabricTransportSettings.MaxMessageSize = this.GetAndValidateMaxMessageSize(fabricTransportSettings.MaxMessageSize);
            fabricTransportSettings.OperationTimeout = this.GetAndValidateOperationTimeout(fabricTransportSettings.OperationTimeout);
            fabricTransportSettings.KeepAliveTimeout = this.GetKeepAliveTimeout(fabricTransportSettings.KeepAliveTimeout);
            var exceptionHandlers = ExceptionHandlerTypes?
                .Where(exceptionHandlerType => exceptionHandlerType.GetInterface(nameof(IExceptionHandler), false) != null)
                .Select(exceptionHandlerType => (IExceptionHandler)Activator.CreateInstance(exceptionHandlerType)).ToArray();
            var serviceRemotingClientFactory = (IServiceRemotingClientFactory)new FabricTransportServiceRemotingClientFactory(
                fabricTransportSettings: fabricTransportSettings,
                callbackClient: callbackClient,
                servicePartitionResolver: (IServicePartitionResolver)null,
                exceptionHandlers: exceptionHandlers,
                traceId: (string)null);

            return new AuditedFabricTransportServiceRemotingClientFactory(serviceRemotingClientFactory);
        }

        private long GetAndValidateMaxMessageSize(long maxMessageSize)
        {
            return this.MaxMessageSize <= 0L ? maxMessageSize : this.MaxMessageSize;
        }

        private TimeSpan GetAndValidateOperationTimeout(TimeSpan operationTimeout)
        {
            return this.OperationTimeoutInSeconds <= 0L ? operationTimeout : TimeSpan.FromSeconds((double)this.OperationTimeoutInSeconds);
        }

        private TimeSpan GetKeepAliveTimeout(TimeSpan keepAliveTimeout)
        {
            return this.KeepAliveTimeoutInSeconds <= 0L ? keepAliveTimeout : TimeSpan.FromSeconds((double)this.KeepAliveTimeoutInSeconds);
        }

        /// <summary>
        ///  FabricTransportSettings returns the default Settings .Loads the configuration file from default Config Package"Config" , if not found then try to load from  default config file "ClientExeName.Settings.xml"  from Client Exe directory.
        /// </summary>
        /// <param name="sectionName">Name of the section within the configuration file. If not found section in configuration file, it will return the default Settings</param>
        /// <returns></returns>
        private static FabricTransportSettings GetDefaultFabricTransportSettings(string sectionName = "TransportSettings")
        {
            var settings = (FabricTransportSettings)null;
            if (!FabricTransportSettings.TryLoadFrom(sectionName, out settings, (string)null, (string)null))
            {
                settings = new FabricTransportSettings();
            }
            return settings;
        }

        private static FabricTransportListenerSettings GetDefaultFabricTransportListenerSettings(string sectionName = "TransportSettings")
        {
            var listenerSettings = (FabricTransportListenerSettings)null;
            if (!FabricTransportListenerSettings.TryLoadFrom(sectionName, out listenerSettings, (string)null))
            {
                listenerSettings = new FabricTransportListenerSettings();
            }
            return listenerSettings;
        }
    }
}