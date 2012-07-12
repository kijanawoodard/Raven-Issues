using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Raven.Client.Linq;
using Xunit;

namespace RavenIssues
{
    public class OrderDetailsTest : IDisposable
    {
        protected EmbeddableDocumentStore DocumentStore { get; private set; }
        protected IDocumentSession Session { get; private set; }

        public OrderDetailsTest()
        {
            DocumentStore = new EmbeddableDocumentStore {RunInMemory = true};
            DocumentStore.RegisterListener(new NoStaleQueriesAllowed());
            DocumentStore.Initialize();
            IndexCreation.CreateIndexes(typeof (OrderDetailsTestOrderIndex).Assembly, DocumentStore);

            Session = DocumentStore.OpenSession();

            Setup();
        }

        private void Setup()
        {
            var customer = new Customer {Name = "Tom"};

            Session.Store(customer);

            var note = new Note {CustomerId = customer.Id};
            Session.Store(note, customer.Id + @"/note");

            var products = new List<Product>
                           {
                               new Product() {Description = "Salmon"},
                               new Product() {Description = "Tuna"},
                               new Product() {Description = "Tilapia"}
                           };

            products.ForEach(x => Session.Store(x));

            var orders = new List<Order>
                         {
                             new Order
                             {
                                 OrderDetails = new List<OrderDetail>
                                                {
                                                    new OrderDetail {ProductId = products[0].Id, Quantity = 2},
                                                    new OrderDetail {ProductId = products[2].Id, Quantity = 12}
                                                }
                             },
                             new Order
                             {
                                 OrderDetails = new List<OrderDetail>
                                                {
                                                    new OrderDetail {ProductId = products[1].Id, Quantity = 15},
                                                    new OrderDetail {ProductId = products[2].Id, Quantity = 3}
                                                }
                             },
                             new Order
                             {
                                 OrderDetails = new List<OrderDetail>
                                                {
                                                    new OrderDetail {ProductId = products[0].Id, Quantity = 12},
                                                    new OrderDetail {ProductId = products[1].Id, Quantity = 12},
                                                    new OrderDetail {ProductId = products[2].Id, Quantity = 12}
                                                }
                             }
                         };

            orders.ForEach(x => Session.Store(x, customer.Id + @"/order/"));

            Session.SaveChanges();
        }

        public void Dispose()
        {
            Session.Dispose();
            DocumentStore.Dispose();
        }

        public class Customer
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        public class Note
        {
            public string Id { get; set; }
            public string CustomerId { get; set; }
        }

        public class Order
        {
            public string Id { get; set; }
            public ICollection<OrderDetail> OrderDetails { get; set; }
        }

        public class OrderDetail
        {
            public string ProductId { get; set; }
            public int Quantity { get; set; }
        }

        public class Product
        {
            public string Id { get; set; }
            public string Description { get; set; }
        }

        internal class OrderDetailsTestOrderIndex :
            AbstractIndexCreationTask<Order, OrderDetailsTestOrderIndex.TransformResult>
        {
            public class TransformResult
            {
                public string OrderId { get; set; }
                public string ProductId { get; set; }
                public int Quantity { get; set; }
                public string Description { get; set; }

                public override string ToString()
                {
                    return string.Format("OrderId: {0}, ProductId: {1}, Quantity: {2}, Description: {3}", OrderId,
                                         ProductId, Quantity, Description);
                }
            }

            public OrderDetailsTestOrderIndex()
            {
                Map = orders => from order in orders
                                from detail in order.OrderDetails
                                select new
                                       {
                                           OrderId = order.Id,
                                           detail.ProductId,
                                           Description = "",
                                           detail.Quantity
                                       };

                TransformResults = (database, results) => from result in results
                                                          let product = database.Load<Product>(result.ProductId)
                                                          select new
                                                                 {
                                                                     result.OrderId,
                                                                     result.ProductId,
                                                                     product.Description,
                                                                     result.Quantity
                                                                 };

                Store(x => x.OrderId, FieldStorage.Yes);
                Store(x => x.ProductId, FieldStorage.Yes);
                Store(x => x.Quantity, FieldStorage.Yes);
            }
        }

        [Fact]
        public void ShouldBeAbleToFindOrdersAndProducts()
        {
            var orders =
                Session
                    .Query<Order>()
                    .ToList();

            var products =
                Session
                    .Query<Product>()
                    .ToList();

            Assert.Equal(3, orders.Count);
            Assert.Equal(3, products.Count);
        }

        [Fact]
        public void ShouldBeAbleToFindDetails()
        {
            var details =
                Session
                    .Query<OrderDetailsTestOrderIndex.TransformResult, OrderDetailsTestOrderIndex>()
                    .Where(x => x.Quantity > 10)
                    .AsProjection<OrderDetailsTestOrderIndex.TransformResult>()
                    .ToList();

            details.ForEach(Console.WriteLine);

            Assert.Equal(5, details.Count);
        }
    }
}