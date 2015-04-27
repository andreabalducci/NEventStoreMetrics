using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Metrics;
using NEventStore;
using NEventStore.Persistence.MongoDB;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Serialization;
using Timer = Metrics.Timer;

namespace NEventStoreMetrics
{
    class Program
    {
        private const int Iterations = 100000;
        private static string endpoint = "http://localhost:1234/";
        private static readonly Counter counter = Metric.Counter("Inserts", Unit.Items);
        private static readonly Counter concurrency = Metric.Counter("Concurrency Ex", Unit.Errors);
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
                .WithSystemCounters()
            ;

            Console.Clear();
            Console.WriteLine("Metrics on {0}", endpoint);
        }

        private static void ConfigureNES()
        {
            Wireup wireup = Wireup.Init();
            //PersistenceWireup persistenceWireup = ConfigureSql(wireup);
            PersistenceWireup persistenceWireup = ConfigureMongo(wireup);
            counter.Increment(Iterations);

            using (
                var storeEvents =
                    persistenceWireup.InitializeStorageEngine()
                    .UsingBinarySerialization()
                    .Build())
            {
                storeEvents.Advanced.Purge();

                Parallel.For(1, Iterations, new ParallelOptions{MaxDegreeOfParallelism = 4}, x =>
                    {
                        using (timer.NewContext())
                        {
                            do
                            {
                                try
                                {
                                    var streamId = x%10;
                                    var stream = storeEvents.OpenStream("default", streamId.ToString(), 0, int.MaxValue);
                                    stream.Add(new EventMessage()
                                    {
                                        Body = "abc"
                                    });
                                    stream.CommitChanges(Guid.NewGuid());
                                    break;
                                }
                                catch (ConcurrencyException ex)
                                {
                                    concurrency.Increment();
                                }
                            } while (true);
                        }
                        counter.Decrement();
                    });

                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
            }
        }

        private static PersistenceWireup ConfigureSql(Wireup wireup)
        {
            return wireup
                .UsingSqlPersistence("SqlServer")
                .WithDialect(new MsSqlDialect());
        }

        private static PersistenceWireup ConfigureMongo(Wireup wireup)
        {
            return wireup
                .UsingMongoPersistence("Mongo", new DocumentObjectSerializer(),
                new MongoPersistenceOptions()
                {
                    ServerSideOptimisticLoop = true
                });
        }
    }
}
