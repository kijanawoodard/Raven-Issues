using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Raven.Client.Document;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using System.Reflection;
using Raven.Abstractions.Indexing;
using System.Linq;
using RavenIssues;
using Raven.Client.Linq;


namespace HotelRatings.Tests
{
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class Post
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string AuthorId { get; set; }
    }

    public class UserPostingStats
    {
        public string UserName { get; set; }
        public string UserId { get; set; }
        public int PostCount { get; set; }
        public double AverageCount { get; set; }
    }

    public class PostCountsByUser_WithName : AbstractMultiMapIndexCreationTask<UserPostingStats>
    {
        public PostCountsByUser_WithName()
        {
            AddMap<User>(users => from user in users
                                  select new
                                  {
                                      UserId = user.Id,
                                      UserName = user.Name,
                                      PostCount = 0,
                                      AverageCount = 0
                                  });

            AddMap<Post>(posts => from post in posts
                                  select new
                                  {
                                      UserId = post.AuthorId,
                                      UserName = (string)null,
                                      PostCount = 1,
                                      AverageCount = 0
                                  });

            Reduce = results => from result in results
                                group result by result.UserId
                                into g
                                select new
                                {
                                    UserId = g.Key,
                                    UserName = g.Select(x => x.UserName).FirstOrDefault(x => x != null),
                                    PostCount = g.Sum(x => x.PostCount),
                                    //AverageCount = g.Any(x => x.PostCount > 0) ? g.Where(x => x.PostCount > 0).Average(x => x.PostCount) : 0
                                    AverageCount = g.Sum(x => x.PostCount) * 1.0 / g.Count(x => x.PostCount > 0)
                                    //AverageCount = g.Select(x => (double)(x.PostCount * 1.0)).Where(x => x > 0).Average()
                                };

            Index(x => x.UserName, FieldIndexing.Analyzed);
        }
    }

    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var store = new EmbeddableDocumentStore() { RunInMemory = true };
            //var store = new DocumentStore { Url = "http://localhost:8080", DefaultDatabase = "HotelRatingsRedux" };
            
            store.RegisterListener(new NoStaleQueriesAllowed());
            store.Initialize();

            IndexCreation.CreateIndexes(Assembly.GetAssembly(typeof(PostCountsByUser_WithName)), store);

            using (var session = store.OpenSession())
            {
                if (!session.Query<User>().Any())
                {
                    session.Store(new User
                                  {
                                      Id = "1",
                                      Name = "Dor Raba"
                                  });

                    session.Store(new Post
                                  {
                                      AuthorId = "1",
                                      Title = "Hello",
                                  });
                    session.Store(new Post
                                  {
                                      AuthorId = "1",
                                      Title = "World",
                                  });
                    session.SaveChanges();
                }
            }

            using (var session = store.OpenSession())
            {
                var ups = 
                    session
                        .Query<UserPostingStats, PostCountsByUser_WithName>()
                        .ToList();

                Assert.AreEqual(1, ups.Count);

                Assert.AreEqual(2, ups[0].PostCount);
                Assert.AreEqual(1.0, ups[0].AverageCount);
                Assert.AreEqual("Dor Raba", ups[0].UserName);
            }

        }
    }
}