using System;
using Raven.Client.Document;
using Raven.Client.Embedded;
using Raven.Json.Linq;
using Xunit;

namespace RavenIssues
{
    public class AggressiveCaching
    {
        [Fact]
        public void Can_Aggressively_Cache()
        {
            using (var store = new DocumentStore
			{
				Url = "http://localhost:8081"
			}.Initialize())
            {
                var customer = new Customer
                               {
                                   Name = "Matt",
                                   Age = 32
                               };

                store.JsonRequestFactory.LogRequest +=
                    (sender, args) => Console.WriteLine("Http Request: {0} status={1}", args.Url, args.Status);

                using (var session = store.OpenSession())
                {
                    //This is never cached, because it's a "PUT" not a "GET"
                    session.Store(customer);
                    session.SaveChanges();
                    var sessionVer = session.Load<Customer>(customer.Id);
                    Console.WriteLine("Session Version:\n\t{0}: Name \"{1}\", Age={2}\n",
                                      sessionVer.Id, sessionVer.Name, sessionVer.Age);
                }

                using (store.AggressivelyCacheFor(TimeSpan.FromMinutes(30)))
                {
                    var dbaseVer1 = store.DatabaseCommands.Get(customer.Id);
                    Console.WriteLine("Database Version (AggressivelyCacheFor 30 mins):\n\t{0}: Name \"{1}\", Age={2}\n",
                                      dbaseVer1.Key, dbaseVer1.ToJson()["Name"], dbaseVer1.ToJson()["Age"]);
                }

                store.DatabaseCommands.Put(customer.Id, null,
                                           RavenJObject.FromObject(new Customer { Name = "Matt Warren", Age = 99 }),
                                           new RavenJObject());
                Console.WriteLine("Changed Database Version, Name = \"Matt Warren\", Age = 99\n");

                using (var session = store.OpenSession())
                {
                    using (store.AggressivelyCacheFor(TimeSpan.FromMinutes(30)))
                    {
                        var newCustomer = session.Load<Customer>(customer.Id);
                        Console.WriteLine("Session Version (Without DisableAggressiveCaching):\n\t{0}: Name \"{1}\", Age={2}\n",
                                          newCustomer.Id, newCustomer.Name, newCustomer.Age);
                    }
                }

                using (var session = store.OpenSession())
                {
                    using (store.DisableAggressiveCaching())
                    {
                        var newCustomer = session.Load<Customer>(customer.Id);
                        Console.WriteLine("Session Version (DisableAggressiveCaching):\n\t{0}: Name \"{1}\", Age={2}\n",
                                          newCustomer.Id, newCustomer.Name, newCustomer.Age);
                    }
                }

                using (var session = store.OpenSession())
                {
                    using (store.AggressivelyCacheFor(TimeSpan.FromMinutes(30)))
                    {
                        var newCustomer = session.Load<Customer>(customer.Id);
                        Console.WriteLine("Session Version (Without DisableAggressiveCaching):\n\t{0}: Name \"{1}\", Age={2}\n",
                                          newCustomer.Id, newCustomer.Name, newCustomer.Age);
                    }
                }

                Console.ReadLine();
            }
        }

        class Customer
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
        }
    }
}