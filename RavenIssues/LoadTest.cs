using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Embedded;
using Xunit;

namespace RavenIssues
{
    public class LoadTest : IDisposable
    {
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public LoadTest()
        {
            DocumentStore = new EmbeddableDocumentStore { RunInMemory = true };
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
//            IndexCreation.CreateIndexes(typeof(BlankTestIndex).Assembly, DocumentStore);

            Session = DocumentStore.OpenSession();

            Setup();
        }

        private void Setup()
        {
            var foos =
                Enumerable.Range(1, 5000)
                    .Select(x => new Foo {Id = "tenant/1/foo/"})
                    .ToList();

            foos.ForEach(Session.Store);

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

        [Fact]
        public void SomeFactAboutFoo()
        {
            var foos =
                Session
                    .LoadStartingWith<Foo>("tenant/1", 0, 5000)
                    .ToList();
            
            Assert.Equal(5000, foos.Count);
        }
    }

    public static class SessionExtensions
    {
        public static IEnumerable<T> LoadStartingWith<T>(this IDocumentSession session,
                       string keyPrefix, int start = 0, int pageSize = 25)
        {
            var inMemorySession = session as InMemoryDocumentSessionOperations;
            if (inMemorySession == null)
            {
                throw new InvalidOperationException(
                    "LoadStartingWith(..) only works on InMemoryDocumentSessionOperations");
            }

            return session.Advanced.DatabaseCommands.StartsWith(keyPrefix, start, pageSize)
                        .Select(inMemorySession.TrackEntity<T>)
                        .ToList();
        }
    }
}