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
    public class DeepIndexTests : IDisposable
    {
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public DeepIndexTests()
        {
            DocumentStore = new EmbeddableDocumentStore { RunInMemory = true };
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
            IndexCreation.CreateIndexes(typeof(DeepIndexTests.UserIndex).Assembly, DocumentStore);

            Session = DocumentStore.OpenSession();

            Setup();
        }

        private void Setup()
        {
            var tom = new User()
                      {
                          Shops = new List<Shop>()
                                  {
                                      new Shop(){LoginPlatformId = "Taobao", LoginAccountName = "the account id in Taobao"},
                                      new Shop(){LoginPlatformId = "Paipai", LoginAccountName = "the account id in Paipai"}
                                  }
                      };

            Session.Store(tom);
            Session.SaveChanges();
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        public class User
        {
            public string Id { get; set; }
            public List<Shop> Shops { get; set; }
        }

        public class Shop
        {
            public string LoginPlatformId { get; set; }
            public string LoginAccountName { get; set; }
        }

        internal class UserIndex : AbstractIndexCreationTask<User, UserIndex.MapResult>
        {
            public class MapResult
            {
                public string Id { get; set; }
                public string LoginPlatformId { get; set; }
                public string LoginAccountName { get; set; }
            }

            public UserIndex()
            {
                Map = users => from user in users
                               select new
                                      {
                                          user.Id,
                                          LoginPlatformId = user.Shops.Select(x => x.LoginPlatformId),
                                          LoginAccountName = user.Shops.Select(x => x.LoginAccountName),
                                      };
            }
        }

        [Fact]
        public void ShouldBeAbleToFindUserByLoginInfo()
        {
            var users =
                Session
                    .Query<UserIndex.MapResult, UserIndex>()
                    .Where(x => x.LoginPlatformId == "Taobao" && x.LoginAccountName == "the account id in Taobao")
                    .As<User>()
                    .ToList();

            Assert.Equal(1, users.Count);
        }

        [Fact]
        public void ShouldBeAbleToFindUserByAntoherLoginInfo()
        {
            var users =
                Session
                    .Query<UserIndex.MapResult, UserIndex>()
                    .Where(x => x.LoginPlatformId == "Paipai" && x.LoginAccountName == "the account id in Paipai")
                    .As<User>()
                    .ToList();

            Assert.Equal(1, users.Count);
        }

        [Fact]
        public void ShouldNotFindUserByInvalidLoginInfo()
        {
            var users =
                Session
                    .Query<UserIndex.MapResult, UserIndex>()
                    .Where(x => x.LoginPlatformId == "foo" && x.LoginAccountName == "bar")
                    .As<User>()
                    .ToList();

            Assert.Equal(0, users.Count);
        }
    }
}