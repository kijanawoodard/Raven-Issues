using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Linq;
using Xunit;

namespace RavenIssues
{
    public class SearchTests : IDisposable
    {
        protected DateTimeOffset Now { get; private set; }
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public SearchTests()
        {
            Now = DateTimeOffset.Now;

            DocumentStore = new EmbeddableDocumentStore {RunInMemory = true};
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
            Session = DocumentStore.OpenSession();

            Setup();
        }

        private void Setup()
        {
            var list = new List<Foo>()
                       {
                           new Foo {Data = "Bill"},
                           new Foo {Data = "Bob"},
                           new Foo {Data = "Bobby"},
                           new Foo {Data = "Charles"},
                           new Foo {Data = "Bob Smith"},
                           new Foo {Data = "David"},
                           new Foo {Data = "Eric"},
                           new Foo {Data = "Bob"},
                       };

            list.ForEach(foo => Session.Store(foo));
            Session.SaveChanges();
        }

        [Fact]
        public void SearchWithTermsGetsResults()
        {
            RavenQueryStatistics stats;
            var foos =
                Session
                    .Query<Foo>()
                    .Statistics(out stats)
                    .Search(x => x.Data, "Bob")
                    .ToList();

            Assert.True(foos.Count == 2);
            Assert.True(stats.TotalResults == 2);
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        internal class Foo
        {
            public string Data { get; set; }
        }
    }
}