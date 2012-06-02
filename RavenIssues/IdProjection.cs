using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client;
using Raven.Client.Embedded;
using Xunit;

namespace RavenIssues
{
    public class ProjectionTests : IDisposable
    {
        protected DateTimeOffset Now { get; private set; }
        protected IDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public ProjectionTests()
        {
            Now = DateTimeOffset.Now;

            DocumentStore = new EmbeddableDocumentStore { RunInMemory = true }.Initialize();
            Session = DocumentStore.OpenSession();

            Setup();
        }

        private void Setup()
        {
            var list = new List<Foo>()
                           {
                               new Foo {Data = 1},
                               new Foo {Data = 2},
                               new Foo {Data = 3},
                               new Foo {Data = 4},
                           };

            list.ForEach(foo => Session.Store(foo));
            Session.SaveChanges();
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        [Fact]
        public void ShouldBeAbleToProjectIdOntoAnotherName()
        {
            var foos =
                Session
                    .Query<Foo>()
                    .Where(foo => foo.Data > 1)
                    .Select(foo => new FooWithFooId
                    {
                        FooId = foo.Id,
                        Data = foo.Data
                    })
                    .ToList();

            Xunit.Assert.NotNull(foos[0].FooId);
        }

        [Fact]
        public void ShouldBeAbleToProjectIdOntoAnotherName_AndAnotherFieldNamedIdShouldNotBeAffected()
        {
            var foos =
                Session
                    .Query<Foo>()
                    .Where(foo => foo.Data > 1)
                    .Select(foo => new FooWithFooIdAndId
                    {
                        FooId = foo.Id,
                        Data = foo.Data
                    })
                    .ToList();

            Xunit.Assert.Null(foos[0].Id);
            Xunit.Assert.NotNull(foos[0].FooId);
        }

        //This works as expected
        [Fact]
        public void ShouldBeAbleToProjectIdOntoAnotherFieldCalledId()
        {
            var foos =
                Session
                    .Query<Foo>()
                    .Where(foo => foo.Data > 1)
                    .Select(foo => new FooWithId
                    {
                        Id = foo.Id,
                        Data = foo.Data
                    })
                    .ToList();

            Xunit.Assert.NotNull(foos[0].Id);
        }

        private class Foo
        {
            public string Id { set; get; }
            public int Data { set; get; }
        }

        private class FooWithFooId
        {
            public string FooId { set; get; }
            public int Data { set; get; }
        }

        private class FooWithId
        {
            public string Id { set; get; }
            public int Data { set; get; }
        }

        private class FooWithFooIdAndId
        {
            public string FooId { set; get; }
            public string Id { set; get; }
            public int Data { set; get; }
        }
    }
}
