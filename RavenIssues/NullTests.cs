using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Raven.Client.Linq;
using Xunit;

namespace RavenIssues
{
    public class NullTests : IDisposable
    {
        protected DateTimeOffset Now { get; private set; }
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public NullTests()
        {
            Now = DateTimeOffset.Now;

            DocumentStore = new EmbeddableDocumentStore { RunInMemory = true };
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
            IndexCreation.CreateIndexes(typeof (PlantsByCaughtDate).Assembly, DocumentStore);

            Session = DocumentStore.OpenSession();

            Setup();
        }

        private void Setup()
        {
            var list = new List<Plant>()
                       {
                           new Plant {CaughtDate = DateTime.UtcNow.AddDays(-100)},
                           new Plant {CaughtDate = DateTime.UtcNow.AddDays(-99)},
                           new Plant {CaughtDate = null},
                           new Plant {CaughtDate = DateTime.UtcNow.AddDays(-98)},
                           new Plant {CaughtDate = DateTime.UtcNow.AddDays(-97)},
                           new Plant {CaughtDate = null},
                           new Plant {CaughtDate = DateTime.UtcNow.AddDays(-96)},
                           new Plant {CaughtDate = DateTime.UtcNow.AddDays(-95)},
                       };

            list.ForEach(plant => Session.Store(plant));
            Session.SaveChanges();
        }

        [Fact]
        public void SearchWithTermsGetsResults()
        {
            RavenQueryStatistics stats;
            var plants =
                Session
                    .Query<Plant, PlantsByCaughtDate>()
                    .Statistics(out stats)
                    .Where(x => x.CaughtDate == null)
                    .ToList();

            Assert.True(plants.Count == 2);
            Assert.True(stats.TotalResults == 2);
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        internal class Plant
        {
            public string Id { get; set; }
            public DateTime? CaughtDate { get; set; }
        }

        internal class PlantsByCaughtDate : AbstractIndexCreationTask<Plant>
        {
            public PlantsByCaughtDate()
            {
                Map = plants => from plant in plants
                                select new { plant.CaughtDate };
            }
        }
    }
}