using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Raven.Client.Linq;
using Xunit;

namespace RavenIssues
{
    public class IndexTests : IDisposable
    {
        protected DateTimeOffset Now { get; private set; }
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public IndexTests()
        {
            Now = DateTimeOffset.Now;

            DocumentStore = new EmbeddableDocumentStore { RunInMemory = true };
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
            IndexCreation.CreateIndexes(typeof(PlantsByKeyword).Assembly, DocumentStore);

            Session = DocumentStore.OpenSession();

            Setup();
        }

        private void Setup()
        {
            var list = new List<Plant>()
                       {
                           new Plant {Name = "Rose", Gardner = "Sally"},
                           new Plant {Name = "Potato", Gardner = "John"},
                           new Plant {Name = "Onion", Gardner = "Susan"},
                           new Plant {Name = "Basil", Gardner = "Gary"},
                           new Plant {Name = "Thyme", Gardner = "Rose"},
                           new Plant {Name = "Tomato", Gardner = "John"},
                           new Plant {Name = "Pineapple", Gardner = "Karen"},
                           new Plant {Name = "Blueberry", Gardner = "Bill"}
                       };

            list.ForEach(plant => Session.Store(plant));
            Session.SaveChanges();
        }

        [Fact]
        public void SearchByPlantNameGetsResults()
        {
            RavenQueryStatistics stats;
            var plants =
                Session
                    .Query<ISearch, PlantsByKeyword>()
                    .Statistics(out stats)
                    .Search(x => x.Keyword, "Onion")
                    .ToList();

            Assert.True(plants.Count == 1);
            Assert.True(stats.TotalResults == 1);
        }

        [Fact]
        public void SearchByGardnerGetsResults()
        {
            RavenQueryStatistics stats;
            var plants =
                Session
                    .Query<ISearch, PlantsByKeyword>()
                    .Statistics(out stats)
                    .Search(x => x.Keyword, "Bill")
                    .ToList();

            Assert.True(plants.Count == 1);
            Assert.True(stats.TotalResults == 1);
        }

        [Fact]
        public void SearchByTermThatMatchesBothPlantAndGardnerGetsResults()
        {
            RavenQueryStatistics stats;
            var plants =
                Session
                    .Query<ISearch, PlantsByKeyword>()
                    .Statistics(out stats)
                    .Search(x => x.Keyword, "Rose")
                    .ToList();

            Assert.True(plants.Count == 2);
            Assert.True(stats.TotalResults == 2);
        }

        [Fact]
        public void FooThis()
        {
            using (var session = DocumentStore.OpenSession())
            {
                var entity = new Plant { Name = "Company", Id = "company/1" };
                session.Store(entity);
                session.SaveChanges();
                
                var entity2 = new Plant { Name = "CompanyTwo", Id = "company/1" };
                session.Store(entity2);
                session.SaveChanges();
            }
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        interface ISearch
        {
            string Keyword { get; set; }
        }

        internal class Plant : ISearch
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Gardner { get; set; }
            string ISearch.Keyword { get; set; } //dangler; just here to query the index
        }

        internal class PlantsByKeyword : AbstractIndexCreationTask<Plant>
        {
            public PlantsByKeyword()
            {
                Map = plants => from plant in plants
                                select new
                                       {
                                           Keyword = new string[] {plant.Name, plant.Gardner}
                                       };

                Index(x => (x as ISearch).Keyword, FieldIndexing.Analyzed);
            }
        }
    }
}