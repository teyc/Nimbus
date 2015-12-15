using System;
using System.IO;
using Nimbus.Configuration;
using Nimbus.Configuration.LargeMessages;
using Nimbus.Infrastructure;
using Nimbus.Infrastructure.Logging;
using Nimbus.LargeMessages.FileSystem.Configuration;
using Nimbus.Ninject.Configuration;
using Nimbus.Transports.WindowsServiceBus;
using Ninject;
using NUnit.Framework;

namespace Nimbus.Extensions.IntegrationTests
{
    [TestFixture]
    public class WhenBuildingABusWithNinjectAndFileSystemMessageStorage
    {
        [Test]
        public void NothingShouldGoBang()
        {
            using (var container = new StandardKernel())
            {
                var typeProvider = new AssemblyScanningTypeProvider();

                container.Bind<ILogger>().To<ConsoleLogger>().InSingletonScope();

                container.RegisterNimbus(typeProvider);

                var largeMessageBodyTempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Guid.NewGuid().ToString());

                container.Bind<ILargeMessageBodyStore>()
                         .ToMethod(
                             c =>
                                 new FileSystemStorageBuilder().Configure()
                                                               .WithStorageDirectory(largeMessageBodyTempPath)
                                                               .WithLogger(c.Kernel.Get<ILogger>())
                                                               .Build())
                         .InSingletonScope();

                container.Bind<IBus>()
                         .ToMethod(
                             c =>
                                 new BusBuilder().Configure()
                                                 .WithTransport(new WindowsServiceBusTransportConfiguration()
                                                                    .WithConnectionString(
                                                                        @"Endpoint=sb://shouldnotexist.example.com/;SharedAccessKeyName=IntegrationTestHarness;SharedAccessKey=borkborkbork=")
                                                                    .WithLargeMessageStorage(new LargeMessageStorageConfiguration()
                                                                                                 .WithMaxSmallMessageSize(50*1024)
                                                                                                 .WithMaxLargeMessageSize(1024*1024))
                                 )
                                                 .WithNames("IntegrationTestHarness", Environment.MachineName)
                                                 .WithTypesFrom(typeProvider)
                                                 .WithDefaultTimeout(TimeSpan.FromSeconds(10))
                                                 .WithLogger(c.Kernel.Get<ILogger>())
                                                 .Build())
                         .InSingletonScope();

                container.Get<IBus>();
            }
        }
    }
}