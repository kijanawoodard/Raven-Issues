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
    public class RelatedItemTests : IDisposable
    {
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        private User Tom { get; set; }

        public RelatedItemTests()
        {
            DocumentStore = new EmbeddableDocumentStore { RunInMemory = true };
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
            IndexCreation.CreateIndexes(typeof(RelatedItemTestsUserIndex).Assembly, DocumentStore);

            Session = DocumentStore.OpenSession();

            Setup();
        }

        private void Setup()
        {
            var dick = new User();
            var harry = new User();

            Session.Store(dick);
            Session.Store(harry);

            Tom = new User()
                      {
                          CoWorkers = new List<CoWorker>()
                                  {
                                      new CoWorker(){UserId = dick.Id, ShowUpdates = true},
                                      new CoWorker(){UserId = harry.Id, ShowUpdates = true},
                                  }
                      };

            Session.Store(Tom);


            var activities = new List<Activity>
                             {
                                 new Activity()
                                 {
                                     UserId = dick.Id,
                                     Text = "See Dick run"
                                 },
                                 new Activity()
                                 {
                                     UserId = harry.Id,
                                     Text = "See Harry run"
                                 }
                             };

            activities.ForEach(x => Session.Store(x));
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
            public List<CoWorker> CoWorkers { get; set; }

            public User()
            {
                CoWorkers = new List<CoWorker>();
            }
        }

        public class CoWorker
        {
            public string UserId { get; set; }
            public bool ShowUpdates { get; set; }
        }

        public class Activity
        {
            public string Id { get; set; }
            public string UserId { get; set; }
            public string Text { get; set; }
        }

        internal class RelatedItemTestsUserIndex : AbstractIndexCreationTask<User>
        {
            public RelatedItemTestsUserIndex()
            {
                Map = users => from user in users
                               select new
                                      {
                                          user.Id,
                                      };
            }
        }

        internal class AcitivtyIndex : AbstractIndexCreationTask<Activity>
        {
            public AcitivtyIndex()
            {
                Map = activities => from activity in activities
                               select new
                               {
                                   activity.Id,
                                   activity.UserId,
                                   activity.Text
                               };
            }
        }

        [Fact]
        public void ShouldBeAbleToFindUserByLoginInfo()
        {
            var user =
                Session
                    .Load<User>(Tom.Id); //I could use Tom below, but we'd have to do this in a real query

            var activities =
                Session
                    .Query<Activity>()
                    .Where(x => x.UserId.In(user.CoWorkers.Where(c => c.ShowUpdates).Select(c => c.UserId)))
                    .ToList();

            Assert.Equal(2, activities.Count);
        }
    }
}