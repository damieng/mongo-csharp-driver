﻿/* Copyright 2010-present MongoDB Inc.
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentAssertions;
using MongoDB.Driver.Linq;
using MongoDB.Driver.TestHelpers;
using Xunit;

namespace MongoDB.Driver.Tests.Linq.Linq3Implementation.Jira
{
    public class CSharp4405Tests : LinqIntegrationTest<CSharp4405Tests.ClassFixture>
    {
        public CSharp4405Tests(ClassFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void Week_should_work()
        {
            var collection = Fixture.Collection;

            var queryable = collection
                .AsQueryable()
                .Select(x => x.D.Week());

            var stages = Translate(collection, queryable);
            AssertStages(stages, "{ $project : { _v : { $week : '$D' }, _id : 0 } }");

            var results = queryable.ToList();
            results.Should().Equal(0, 0);
        }

        [Fact]
        public void Week_with_timezone_should_work()
        {
            var collection = Fixture.Collection;

            var queryable = collection
                .AsQueryable()
                .Select(x => x.D.Week(x.TZ));

            var stages = Translate(collection, queryable);
            AssertStages(stages, "{ $project : { _v : { $week : { date : '$D', timezone : '$TZ' } }, _id : 0 } }");

            var results = queryable.ToList();
            results.Should().Equal(0, 52);
        }

        public class C
        {
            public int Id { get; set; }
            public DateTime D { get; set; }
            public string TZ { get; set; }
        }

        public sealed class ClassFixture : MongoCollectionFixture<C>
        {
            protected override IEnumerable<C> InitialData =>
            [
                new C { Id = 1, D = DateTime.Parse("2022-01-01T00:00:00Z", null, DateTimeStyles.AdjustToUniversal), TZ = "UTC" },
                new C { Id = 2, D = DateTime.Parse("2022-01-01T00:00:00Z", null, DateTimeStyles.AdjustToUniversal), TZ = "America/New_York" }
            ];
        }
    }
}
