/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System.Linq;
using FluentAssertions;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.TestHelpers.XunitExtensions;
using MongoDB.Driver.TestHelpers;
using Xunit;

namespace MongoDB.Driver.Tests.Linq.Integration;

public class QueryableJoinSubqueryTests : LinqIntegrationTest<QueryableJoinSubqueryTests.ClassFixture>
{
    public QueryableJoinSubqueryTests(ClassFixture fixture)
        : base(fixture)
    {
    }

    // A global Take in the inner must materialize before the join: the top-3 orders {1,2,3} all belong
    // to Alice, so the join yields 3 rows, not a per-customer top-3.
    [Fact]
    public void Join_with_global_Take_in_inner_subquery_should_apply_limit_before_join()
    {
        RequireServer.Check().Supports(Feature.LookupConciseSyntax);

        var customers = Fixture.CustomersCollection;

        var globalTop3 = Fixture.OrdersCollection.AsQueryable().OrderBy(o => o.Id).Take(3);

        var queryable = customers.AsQueryable()
            .Join(globalTop3, c => c.Id, o => o.CustomerId, (c, o) => new { c.Name, OrderId = o.Id });

        var stages = Translate(customers, queryable);
        AssertStages(
            stages,
            "{ $project : { _outer : '$$ROOT', _id : 0 } }",
            "{ $lookup : { from : 'orders', let : { outer_key : '$_outer._id' }, pipeline : [{ $sort : { _id : 1 } }, { $limit : 3 }, { $match : { $expr : { $eq : ['$CustomerId', '$$outer_key'] } } }], as : '_inner' } }",
            "{ $unwind : '$_inner' }",
            "{ $project : { Name : '$_outer.Name', OrderId : '$_inner._id', _id : 0 } }");

        var results = queryable.ToList();

        results.Should().HaveCount(3);
        results.Select(r => r.OrderId).Should().BeEquivalentTo([1, 2, 3]);
        results.Should().OnlyContain(r => r.Name == "Alice");
    }

    // A membership-preserving inner (OrderBy only, no cardinality operator) joins all matching rows.
    [Fact]
    public void Join_with_ordered_only_inner_subquery_should_join_all_matching_rows()
    {
        var customers = Fixture.CustomersCollection;

        var orderedInner = Fixture.OrdersCollection.AsQueryable().OrderBy(o => o.Id);

        var queryable = customers.AsQueryable()
            .Join(orderedInner, c => c.Id, o => o.CustomerId, (c, o) => new { c.Name, OrderId = o.Id });

        var results = queryable.ToList();

        results.Should().HaveCount(6);
        results.Select(r => r.OrderId).Should().BeEquivalentTo([1, 2, 3, 4, 5, 6]);
    }

    // GroupJoin with a membership-preserving inner (Where) correlates via localField/foreignField.
    [Fact]
    public void GroupJoin_with_filtered_inner_subquery_should_group_matching_rows_per_key()
    {
        RequireServer.Check().Supports(Feature.LookupConciseSyntax);

        var customers = Fixture.CustomersCollection;

        var filteredOrders = Fixture.OrdersCollection.AsQueryable().Where(o => o.Id != 4);

        var queryable = customers.AsQueryable()
            .GroupJoin(filteredOrders, c => c.Id, o => o.CustomerId, (c, orders) => new { c.Name, OrderIds = orders.Select(o => o.Id) });

        var stages = Translate(customers, queryable);
        AssertStages(
            stages,
            "{ $project : { _outer : '$$ROOT', _id : 0 } }",
            "{ $lookup : { from : 'orders', localField : '_outer._id', foreignField : 'CustomerId', pipeline : [{ $match : { _id : { $ne : 4 } } }], as : '_inner' } }",
            "{ $project : { Name : '$_outer.Name', OrderIds : '$_inner._id', _id : 0 } }");

        var results = queryable.ToList().OrderBy(r => r.Name).ToList();
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Alice");
        results[0].OrderIds.Should().BeEquivalentTo([1, 2, 3]);
        results[1].Name.Should().Be("Bob");
        results[1].OrderIds.Should().BeEquivalentTo([5, 6]);
    }

    // GroupJoin with a global Take gives each key its slice of the global top-3: Alice {1,2,3}, Bob {}.
    [Fact]
    public void GroupJoin_with_global_Take_in_inner_subquery_should_apply_limit_before_join()
    {
        RequireServer.Check().Supports(Feature.LookupConciseSyntax);

        var customers = Fixture.CustomersCollection;

        var globalTop3 = Fixture.OrdersCollection.AsQueryable().OrderBy(o => o.Id).Take(3);

        var queryable = customers.AsQueryable()
            .GroupJoin(globalTop3, c => c.Id, o => o.CustomerId, (c, orders) => new { c.Name, OrderIds = orders.Select(o => o.Id) });

        var stages = Translate(customers, queryable);
        AssertStages(
            stages,
            "{ $project : { _outer : '$$ROOT', _id : 0 } }",
            "{ $lookup : { from : 'orders', let : { outer_key : '$_outer._id' }, pipeline : [{ $sort : { _id : 1 } }, { $limit : 3 }, { $match : { $expr : { $eq : ['$CustomerId', '$$outer_key'] } } }], as : '_inner' } }",
            "{ $project : { Name : '$_outer.Name', OrderIds : '$_inner._id', _id : 0 } }");

        var results = queryable.ToList().OrderBy(r => r.Name).ToList();
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Alice");
        results[0].OrderIds.Should().BeEquivalentTo([1, 2, 3]);
        results[1].Name.Should().Be("Bob");
        results[1].OrderIds.Should().BeEmpty();
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
    }

    public sealed class ClassFixture : MongoDatabaseFixture
    {
        public IMongoCollection<Customer> CustomersCollection { get; private set; }
        public IMongoCollection<Order> OrdersCollection { get; private set; }

        protected override void InitializeFixture()
        {
            CustomersCollection = CreateCollection<Customer>("customers");
            CustomersCollection.InsertMany([
                new Customer { Id = 1, Name = "Alice" },
                new Customer { Id = 2, Name = "Bob" }]);

            // Alice owns orders 1..4, Bob owns 5..6. The 3 globally-lowest order Ids are 1,2,3 — all Alice's.
            OrdersCollection = CreateCollection<Order>("orders");
            OrdersCollection.InsertMany([
                new Order { Id = 1, CustomerId = 1 }, new Order { Id = 2, CustomerId = 1 },
                new Order { Id = 3, CustomerId = 1 }, new Order { Id = 4, CustomerId = 1 },
                new Order { Id = 5, CustomerId = 2 }, new Order { Id = 6, CustomerId = 2 }]);
        }
    }
}
