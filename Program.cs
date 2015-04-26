using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Metrics;
using NEventStore;
using NEventStore.Persistence.Sql.SqlDialects;
using Timer = Metrics.Timer;

namespace NEventStoreMetrics
{
    class Program
    {
        private const int Iterations = 100000;
        private static string endpoint = "http://localhost:1234/";
        private static readonly Counter counter = Metric.Counter("Inserts", Unit.Items);
        private static readonly Counter writers = Metric.Counter("Writers", Unit.Threads);
        private static readonly Timer timer = Metric.Timer("Events", Unit.Events);


        static void Main(string[] args)
        {
            ConfigureMetrics();
            Console.WriteLine("Press any key to start...");
            Console.ReadLine();
            ConfigureNES();
        }

        private static void ConfigureMetrics()
        {
            Metric
                .Config
                .WithHttpEndpoint(endpoint)
            ;

            Console.Clear();
            Console.WriteLine("Metrics on {0}", endpoint);
        }

        private static void ConfigureNES()
        {
            Wireup wireup = Wireup.Init();
            PersistenceWireup persistenceWireup = ConfigurePersistence(wireup);
            counter.Increment(Iterations);

            using (
                var storeEvents =
                    persistenceWireup.InitializeStorageEngine().UsingBinarySerialization().Build())
            {
                storeEvents.Advanced.Purge();

                Parallel.For(1, Iterations, x =>
                    {
                        writers.Increment();
                        using (timer.NewContext())
                        {
                            var stream = storeEvents.OpenStream("default", x.ToString(), 0, int.MaxValue);
                            stream.Add(new EventMessage()
                            {
                                Body = new byte[1024]
                            });
                            stream.CommitChanges(Guid.NewGuid());
                        }
                        counter.Decrement();
                        writers.Decrement();
                    });

                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
            }
        }

        private static PersistenceWireup ConfigurePersistence(Wireup wireup)
        {
            return wireup
                .UsingSqlPersistence("SqlServer")
                .WithDialect(new MsSqlDialect());
        }
    }
}
