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
    public class SummarizeTests : IDisposable
    {
        protected DateTimeOffset Now { get; private set; }
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public SummarizeTests()
        {
            Now = DateTimeOffset.Now;

            DocumentStore = new EmbeddableDocumentStore {RunInMemory = true};
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
            IndexCreation.CreateIndexes(typeof(BatchSummary).Assembly, DocumentStore);

            Session = DocumentStore.OpenSession();

            Setup();
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        private void Setup()
        {
            var batches = new List<Batch>()
                          {
                              new Batch {Name = "Morning Batch", JobIds = new[] {"jobs-1", "jobs-2", "jobs-3"}},
                              new Batch {Name = "Afternoon Batch", JobIds = new[] {"jobs-4"}},
                              new Batch {Name = "Night Batch", JobIds = new[] {"jobs-5", "jobs-6", "jobs-7", "jobs-8"}},
                          };

            batches.ForEach(batch => Session.Store(batch));

            var jobs = new List<Job>
                       {
                           new Job() {Id = "jobs-1", IsComplete = false},
                           new Job() {Id = "jobs-2", IsComplete = true},
                           new Job() {Id = "jobs-3", IsComplete = true},
                           new Job() {Id = "jobs-4", IsComplete = false},
                           new Job() {Id = "jobs-5", IsComplete = true},
                           new Job() {Id = "jobs-6", IsComplete = false},
                           new Job() {Id = "jobs-7", IsComplete = true},
                           new Job() {Id = "jobs-8", IsComplete = true},
                           new Job() {Id = "jobs-9", IsComplete = true},
                           new Job() {Id = "jobs-10", IsComplete = true},
                           new Job() {Id = "jobs-11", IsComplete = false}
                       };

            jobs.ForEach(job => Session.Store(job));

            Session.SaveChanges();
        }

        [Fact]
        public void BatchSummaryShouldIncludeProperCounts()
        {
            var xyz = Session.Query<Job>().ToList();
            var c = xyz.Count;
            RavenQueryStatistics stats;
            var batches =
                Session
                    .Query<BatchSummary.Result, BatchSummary>()
                    .Statistics(out stats)
                    //.Where(x => x.CompletedJobCount > 0)

                    .ToList();

            Assert.Equal(2, batches.Count);
            Assert.Equal(2, batches[0].CompletedJobCount);
            Assert.Equal(3, batches[1].CompletedJobCount);
            Assert.Equal(1, stats.TotalResults);
        }

        internal class Batch
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public IEnumerable<string> JobIds { get; set; }
        }

        internal class Job
        {
            public string Id { get; set; }
            public bool IsComplete { get; set; }
        }

        internal class BatchSummary : AbstractMultiMapIndexCreationTask<BatchSummary.Result>
        {
            public class Result
            {
                public string BatchId { get; set; }
                public string BatchName { get; set; }
                public int CompletedJobCount { get; set; }

                public string JobId { get; set; } //throw away; used in reduce
            }

            public BatchSummary()
            {
                AddMap<Job>(jobs => from job in jobs
                                 select new
                                 {
                                     BatchId = (string)null,
                                     BatchName = (string)null,
                                     JobId = job.Id,
                                     CompletedJobCount = job.IsComplete ? 1 : 0
                                 });

                AddMap<Batch>(batches => from batch in batches
                                         from jobId in batch.JobIds
                                         select new
                                         {
                                             BatchId = batch.Id,
                                             BatchName = batch.Name,
                                             JobId = jobId,
                                             CompletedJobCount = 0
                                         });

                Reduce = results =>
                         from result in results
                         let fixup = results.Select(r => new Result
                                                         {
                                                             JobId = r.JobId,
                                                             CompletedJobCount = r.CompletedJobCount,
                                                             BatchId = results
                                                                           .Where(x => x.JobId == r.JobId)
                                                                           .Select(x => x.BatchId)
                                                                           .FirstOrDefault(x => x != null) ??
                                                                       string.Empty,
                                                             BatchName = results
                                                                             .Where(x => x.JobId == r.JobId)
                                                                             .Select(x => x.BatchName)
                                                                             .FirstOrDefault(x => x != null) ??
                                                                         string.Empty,
                                                         })
                         from f in fixup
                         group f by f.BatchId
                         into b
                         select new
                                {
                                    BatchId = b.Key,
                                    JobId = "",
                                    BatchName = "",//b.First().BatchName,
                                    CompletedJobCount = b.Sum(x => x.CompletedJobCount)
                                };







//                         group result by result.JobId
//                         into g
//                         select new
//                                {
//                                    JobId = g.Key,
//                                    CompletedJobCount = g.Sum(x => x.CompletedJobCount),
//                                    BatchId = g.Select(x => x.BatchId).FirstOrDefault(x => x != null) ?? string.Empty,
//                                    BatchName =
//                             g.Select(x => x.BatchName).FirstOrDefault(x => x != null) ?? string.Empty,
//
//                                }//)
//                                group xyz by xyz.BatchId into abc
//                                select new 
//                                       {
//                                           JobId = "",//abc.First().JobId,
//                                           CompletedJobCount = abc.Sum(x => x.CompletedJobCount),
//                                           BatchId = abc.Key,
//                                           BatchName = ""//abc.First().BatchName
//                                       };


//                TransformResults =
//                    (database, results) => from result in results
//                                           let jobs = database.Load<Batch>(result.JobIds)
//                                           select new
//                                           {
//                                               result.Id,
//                                               result.Name,
//                                               result.JobIds,
//                                               CompletedJobCount = jobs.Count(x => x.IsComplete)
//                                           };
//
//                Store(x => x.CompletedJobCount, FieldStorage.Yes);
//                //                Index(x => x.CompletedJobCount, FieldIndexing.Analyzed);
            }
        }

        internal class BatchSummaryFail : AbstractIndexCreationTask<Batch, BatchSummaryFail.Result>
        {
            public class Result
            {
                public string Id { get; set; }
                public string Name { get; set; }
                public IEnumerable<string> JobIds { get; set; }
                public int CompletedJobCount { get; set; }
            }

            public BatchSummaryFail()
            {
                Map = batches => from batch in batches
                                select new
                                {
                                    batch.Id,
                                    batch.Name,
                                    batch.JobIds,
                                    CompletedJobCount = 0
                                };

                TransformResults =
                    (database, results) => from result in results
                                         let jobs = database.Load<Job>(result.JobIds)
                                         select new
                                                {
                                                    result.Id,
                                                    result.Name, 
                                                    result.JobIds,
                                                    CompletedJobCount = jobs.Count(x => x.IsComplete)
                                                };

                Store(x => x.CompletedJobCount, FieldStorage.Yes);
//                Index(x => x.CompletedJobCount, FieldIndexing.Analyzed);
            }
        }
    }

    public class SummarizeTests2 : IDisposable
    {
        protected DateTimeOffset Now { get; private set; }
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public SummarizeTests2()
        {
            Now = DateTimeOffset.Now;

            DocumentStore = new EmbeddableDocumentStore { RunInMemory = true };
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
            IndexCreation.CreateIndexes(typeof(BatchSummary).Assembly, DocumentStore);

            Session = DocumentStore.OpenSession();

            Setup();
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        private void Setup()
        {
            var batches = new List<Batch>()
                          {
                              new Batch {Id = "batches-1", Name = "Morning Batch", JobIds = new[] {"jobs-1", "jobs-2", "jobs-3"}},
                              new Batch {Id = "batches-2", Name = "Afternoon Batch", JobIds = new[] {"jobs-4"}},
                              new Batch {Id = "batches-3", Name = "Night Batch", JobIds = new[] {"jobs-5", "jobs-6", "jobs-7", "jobs-8"}},
                          };

            batches.ForEach(batch => Session.Store(batch));

            var jobs = new List<Job>
                       {
                           new Job() {Id = "jobs-1", BatchId="batches-1", IsComplete = false},
                           new Job() {Id = "jobs-2", BatchId="batches-1", IsComplete = true},
                           new Job() {Id = "jobs-3", BatchId="batches-1", IsComplete = true},
                           new Job() {Id = "jobs-4", BatchId="batches-2", IsComplete = false},
                           new Job() {Id = "jobs-5", BatchId="batches-3", IsComplete = true},
                           new Job() {Id = "jobs-6", BatchId="batches-3", IsComplete = false},
                           new Job() {Id = "jobs-7", BatchId="batches-3", IsComplete = true},
                           new Job() {Id = "jobs-8", BatchId="batches-3", IsComplete = true},
                           new Job() {Id = "jobs-9", IsComplete = true},
                           new Job() {Id = "jobs-10", IsComplete = true},
                           new Job() {Id = "jobs-11", IsComplete = false}
                       };

            jobs.ForEach(job => Session.Store(job));

            Session.SaveChanges();
        }

        [Fact]
        public void BatchSummaryShouldIncludeProperCounts()
        {
            var batches =
                Session
                    .Query<BatchSummary.Result, BatchSummary>()
                    .Where(x => x.CompletedJobCount > 0)
                    .ToList();

            Assert.Equal(2, batches.Count);
            Assert.Equal(2, batches[0].CompletedJobCount);
            Assert.Equal(3, batches[1].CompletedJobCount);
        }

        internal class Batch
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public IEnumerable<string> JobIds { get; set; }
        }

        internal class Job
        {
            public string Id { get; set; }
            public string BatchId { get; set; }
            public bool IsComplete { get; set; }
        }

        internal class BatchSummary : AbstractIndexCreationTask<Job, BatchSummary.Result>
        {
            public class Result
            {
                public string BatchId { get; set; }
                public string BatchName { get; set; }
                public int CompletedJobCount { get; set; }
            }

            public BatchSummary()
            {
                Map = jobs => from job in jobs
                                 select new
                                 {
                                     job.BatchId,
                                     CompletedJobCount = job.IsComplete ? 1 : 0
                                 };

                Reduce = results => from result in results
                                    where result.BatchId != null
                                    group result by result.BatchId into g
                                    select new
                                    {
                                        BatchId = g.Key,
                                        CompletedJobCount = g.Sum(x => x.CompletedJobCount)
                                    };

                TransformResults =
                    (database, results) => from result in results
                                           let batch = database.Load<Batch>(result.BatchId)
                                           select new
                                           {
                                               result.BatchId,
                                               BatchName = batch.Name,
                                               result.CompletedJobCount
                                           };
            }
        }
    }
}