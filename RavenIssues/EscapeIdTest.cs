using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Xunit;

namespace RavenIssues
{
    public class EscapeIdTest : IDisposable
    {
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public EscapeIdTest()
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
            var languages = new List<Language>
                            {
                                new Language
                                {
                                    Name = "C#"
                                },
                                new Language
                                {
                                    Name = "C++"
                                }
                            };

            languages.ForEach(Session.Store);

            Session.SaveChanges();
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        public class Language
        {
            public string Id { get { return "languages/" + Name.Trim(); } }
            public string Name { get; set; }

            public override string ToString()
            {
                return string.Format("Id: {0}, Name: {1}", Id, Name);
            }
        }

        internal class BlankTestIndex :
            AbstractIndexCreationTask<Language>
        {
            public BlankTestIndex()
            {
                Map = languages => from language in languages
                              select new
                                     {
                                         language.Id,
                                         language.Name
                                     };
            }
        }

        [Fact]
        public void CanQueryLanguages()
        {
            var languages =
                Session
                    .Query<Language>()
                    .ToList();

            languages.ForEach(Console.WriteLine);

            Assert.Equal(2, languages.Count);
        }
    }
}