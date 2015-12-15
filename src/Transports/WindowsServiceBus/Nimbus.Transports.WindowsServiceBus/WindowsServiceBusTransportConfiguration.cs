﻿using System;
using System.IO;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Nimbus.ConcurrentCollections;
using Nimbus.Configuration;
using Nimbus.Configuration.Debug.Settings;
using Nimbus.Configuration.LargeMessages;
using Nimbus.Configuration.PoorMansIocContainer;
using Nimbus.Configuration.Settings;
using Nimbus.Configuration.Transport;
using Nimbus.Infrastructure;
using Nimbus.Infrastructure.MessageSendersAndReceivers;
using Nimbus.Transports.WindowsServiceBus.DevelopmentStubs;

namespace Nimbus.Transports.WindowsServiceBus
{
    public class WindowsServiceBusTransportConfiguration : TransportConfiguration
    {
        internal ConnectionStringSetting ConnectionString { get; set; }
        internal ServerConnectionCountSetting ServerConnectionCount { get; set; }
        internal DefaultMessageLockDurationSetting DefaultMessageLockDuration { get; set; }

        internal LargeMessageStorageConfiguration LargeMessageStorageConfiguration { get; set; }

        public WindowsServiceBusTransportConfiguration()
        {
            LargeMessageStorageConfiguration = new LargeMessageStorageConfiguration();
        }

        public WindowsServiceBusTransportConfiguration WithConnectionString(string connectionString)
        {
            ConnectionString = new ConnectionStringSetting {Value = connectionString};
            return this;
        }

        public WindowsServiceBusTransportConfiguration WithConnectionStringFromFile(string filename)
        {
            var connectionString = File.ReadAllText(filename).Trim();
            return WithConnectionString(connectionString);
        }

        public WindowsServiceBusTransportConfiguration WithLargeMessageStorage(LargeMessageStorageConfiguration largeMessageStorageConfiguration)
        {
            LargeMessageStorageConfiguration = largeMessageStorageConfiguration;
            return this;
        }

        public WindowsServiceBusTransportConfiguration WithServerConnectionCount(int serverConnectionCount)
        {
            ServerConnectionCount = new ServerConnectionCountSetting {Value = serverConnectionCount};
            return this;
        }

        public WindowsServiceBusTransportConfiguration WithDefaultMessageLockDuration(TimeSpan defaultLockDuration)
        {
            DefaultMessageLockDuration = new DefaultMessageLockDurationSetting {Value = defaultLockDuration};
            return this;
        }

        protected override void RegisterComponents(PoorMansIoC container)
        {
            LargeMessageStorageConfiguration.RegisterWith(container);
            HackyComponentRegistrationExtensions.RegisterPropertiesFromConfigurationObject(container, LargeMessageStorageConfiguration);

            container.RegisterType<BrokeredMessageFactory>(ComponentLifetime.SingleInstance, typeof(IBrokeredMessageFactory));
            container.RegisterType<WindowsServiceBusTransport>(ComponentLifetime.SingleInstance, typeof (INimbusTransport));
            container.RegisterType<NamespaceCleanser>(ComponentLifetime.SingleInstance);
            container.RegisterType<AzureQueueManager>(ComponentLifetime.SingleInstance, typeof (IQueueManager));
            container.RegisterType<StubDelayedDeliveryService>(ComponentLifetime.SingleInstance, typeof (IDelayedDeliveryService));

            var namespaceManagerRoundRobin = new RoundRobin<NamespaceManager>(
                container.Resolve<ServerConnectionCountSetting>(),
                () =>
                {
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(container.Resolve<ConnectionStringSetting>());
                    namespaceManager.Settings.OperationTimeout = TimeSpan.FromSeconds(120);
                    return namespaceManager;
                },
                nsm => false,
                nsm => { });

            container.Register<Func<NamespaceManager>>(c => namespaceManagerRoundRobin.GetNext);

            var messagingFactoryRoundRobin = new RoundRobin<MessagingFactory>(
                container.Resolve<ServerConnectionCountSetting>(),
                () =>
                {
                    var messagingFactory = MessagingFactory.CreateFromConnectionString(container.Resolve<ConnectionStringSetting>());
                    messagingFactory.PrefetchCount = container.Resolve<ConcurrentHandlerLimitSetting>();
                    return messagingFactory;
                },
                mf => mf.IsBorked(),
                mf => { });

            container.Register<Func<MessagingFactory>>(c => messagingFactoryRoundRobin.GetNext);

            if (container.Resolve<RemoveAllExistingNamespaceElementsSetting>())
            {
                var namespaceCleanser = container.Resolve<NamespaceCleanser>();
                namespaceCleanser.RemoveAllExistingNamespaceElements().Wait();
            }
        }
    }
}