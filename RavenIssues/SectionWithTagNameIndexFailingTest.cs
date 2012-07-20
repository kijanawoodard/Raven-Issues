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
    public class SectionWithTagNameIndexFailingTest : IDisposable
    {
        public IDocumentStore DocumentStore { get; set; }
//        public IDocumentSession DocumentSession { get; set; }

        public SectionWithTagNameIndexFailingTest()
        {
            DocumentStore = new EmbeddableDocumentStore() { RunInMemory = true };
            DocumentStore.Initialize();

            IndexCreation.CreateIndexes(typeof(SectionWithTagNameIndexFailing).Assembly, DocumentStore);

            using (var session = DocumentStore.OpenSession())
            {
                var tag = new Tag();
                tag.Name = "test";
                tag.IsChildFriendly = false;

                session.Store(tag);


                var section = new OnlineStoreSection();
                section.Audience = new TargetAudience();
                section.Audience.Ages = new[] { 1, 2, 3, 4, 5 };
                section.Audience.Sexes = new[] { "Male", "Female", "Unsure" };

                section.Tags = new string[] { tag.Id };

                session.Store(section);


                session.SaveChanges();
            }
        }

        public void Dispose()
        {
//            DocumentSession.Dispose();
            DocumentStore.Dispose();
        }

        [Fact]
        public void Should_Give_Me_AllMales()
        {
            using (var session = DocumentStore.OpenSession())
            {
                var results = 
                    session
                        .Query<SectionWithTagNameIndexFailing.ReduceResult, SectionWithTagNameIndexFailing>()
                        .Customize(x => x.WaitForNonStaleResults())
                        .Where(x => x.Sexes.Any(s => s == "Male"))
                        .ToList();

                Assert.NotNull(results);
                Assert.Equal(1, results.Count());

                var first = results.First();
                Assert.NotNull(first);
                Assert.NotNull(first.Sexes);
                Assert.NotNull(first.Ages);
                Assert.Contains("Male", first.Sexes);
                Assert.Equal(new[] { 1, 2, 3, 4, 5 }, first.Ages);
                Assert.Contains("test", first.Tags);
            }
        }

        public class SectionWithTagNameIndexFailing : AbstractIndexCreationTask<OnlineStoreSection, SectionWithTagNameIndexFailing.ReduceResult>
        {
            public class ReduceResult
            {
                public IEnumerable<string> Sexes { get; set; }
                public IEnumerable<int> Ages { get; set; }
                public IEnumerable<string> Tags { get; set; }
                public DateTime CreatedOn { get; set; }
                public string Id { get; set; }
                public string LocationId { get; set; }
                public string OnlineStoreId { get; set; }
                public string Url { get; set; }

                public IEnumerable<string> Foo { get; set; }
            }

            public SectionWithTagNameIndexFailing()
            {
                Map = results => from result in results
                                 select new
                                 {
                                     result.Audience.Sexes,
                                     result.Audience.Ages,
                                     result.CreatedOn,
                                     result.Id,
                                     result.LocationId,
                                     result.OnlineStoreId,
                                     result.Tags,
                                     result.Url
                                 };

                TransformResults = (database, results) => from result in results
                                                         let tag = database.Load<Tag>(result.Tags)
                                                         let doc = database.Load<OnlineStoreSection>(result.Id)
                                                         select new
                                                         {
                                                             doc.Audience.Sexes,
                                                             doc.Audience.Ages,
                                                             result.CreatedOn,
                                                             result.Id,
                                                             result.LocationId,
                                                             result.OnlineStoreId,
                                                             Tags = tag.Select(a => a.Name),
                                                             Foo = result.Tags,
                                                             result.Url
                                                         };

                Indexes.Add(a => a.Sexes, FieldIndexing.Analyzed);
            }
        }

        public class Tag
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public bool IsChildFriendly { get; set; }
        }

        public class OnlineStoreSection
        {
            public string Id { get; set; }
            public DateTime CreatedOn { get; set; }
            public string OnlineStoreId { get; set; }
            public string LocationId { get; set; }

            public ICollection<string> Tags { get; set; }

            public string Url { get; set; }

            public TargetAudience Audience { get; set; }
        }

        public class TargetAudience
        {
            public IEnumerable<string> Sexes { get; set; }
            public IEnumerable<int> Ages { get; set; }
        }
    }
}