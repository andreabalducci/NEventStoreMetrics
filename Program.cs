using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Metrics;
using NEventStore;
using NEventStore.Persistence.Sql.SqlDialects;

namespace NEventStoreMetrics
{
    class Program
    {
        private static string endpoint = "http://localhost:1234/";
        private static readonly Counter counter = Metric.Counter("Inserts", Unit.Requests);

        static void Main(string[] args)
        {
            ConfigureMetrics();
            Console.WriteLine("Press any key to start...");
            Console.ReadLine();
            ConfigureNES();
            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        private static void ConfigureMetrics()
        {
            Metric
                .Config
                .WithHttpEndpoint(endpoint)
                .WithAllCounters();

            Console.Clear();
            Console.WriteLine("Metrics on {0}", endpoint);
        }

        private static void ConfigureNES()
        {
            Wireup wireup = Wireup.Init();
            PersistenceWireup persistenceWireup = ConfigurePersistence(wireup);


            using (
                IStoreEvents storeEvents =
                    persistenceWireup.InitializeStorageEngine().UsingBinarySerialization().Build())
            {
                storeEvents.Advanced.Purge();

                for(int c = 1 ; c <= 1000; c++)
                {
                    var stream = storeEvents.OpenStream("default", c.ToString(),0,int.MaxValue);
                    stream.Add(new EventMessage()
                    {
                        Body = new byte[1024]
                    });
                    stream.CommitChanges(Guid.NewGuid());
                    counter.Increment();
                }
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
