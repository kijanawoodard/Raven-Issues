using System;
using System.Linq;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Xunit;

namespace RavenIssues
{
    public class BlankTest : IDisposable
    {
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public BlankTest()
        {
            DocumentStore = new EmbeddableDocumentStore { RunInMemory = true };
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
            IndexCreation.CreateIndexes(typeof(BlankTestIndex).Assembly, DocumentStore);

            Session = DocumentStore.OpenSession();

            Setup();
        }

        private void Setup()
        {
            

            Session.SaveChanges();
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        public class Foo
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        internal class BlankTestIndex :
            AbstractIndexCreationTask<Foo, BlankTestIndex.TransformResult>
        {
            public class TransformResult
            {
                public string Id { get; set; }
                public string Name { get; set; }
            }

            public BlankTestIndex()
            {
                Map = foos => from foo in foos
                              select new
                                     {
                                         foo.Id,
                                         foo.Name
                                     };

                TransformResults = (database, results) => from result in results
                                                          select new
                                                                 {
                                                                     result.Id,
                                                                     result.Name
                                                                 };
            }
        }

        [Fact]
        public void SomeFactAboutFoo()
        {
            
        }
    }
}