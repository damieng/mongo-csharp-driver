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
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Linq.Linq3Implementation.Ast;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Filters;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Stages;
using MongoDB.Driver.Linq.Linq3Implementation.ExtensionMethods;

namespace MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToPipelineTranslators
{
    // Shared translation logic for Join, LeftJoin and GroupJoin: resolving the inner sequence (a bare
    // collection or a subquery) and building the $lookup stage in a way that keeps a global cardinality
    // operator in the inner subquery from being pushed below the join correlation.
    internal static class JoinTranslationHelper
    {
        // Stages that map each input document to exactly one output document without changing which documents
        // are present (filter, reorder, reshape). A cardinality operator (Take/Skip/Distinct/etc.) produces a
        // stage outside this set, and its result depends on seeing the whole inner sequence, so it must not be
        // pushed below the join correlation.
        private static readonly AstNodeType[] __membershipPreservingStages =
        {
            AstNodeType.MatchStage,
            AstNodeType.SortStage,
            AstNodeType.ProjectStage,
            AstNodeType.AddFieldsStage,
            AstNodeType.SetStage,
            AstNodeType.UnsetStage
        };

        // Resolves the inner sequence to its collection name and output serializer. When the inner is a subquery
        // (not a bare collection) its translated stages are returned as FilterPipeline, otherwise FilterPipeline
        // is null.
        public static (string CollectionName, IBsonSerializer Serializer, AstPipeline FilterPipeline) ResolveInner(
            TranslationContext context, Expression containerExpression, Expression innerExpression)
        {
            if (innerExpression is ConstantExpression)
            {
                var (name, serializer) = innerExpression.GetCollectionInfoFromQueryable(containerExpression);
                return (name, serializer, null);
            }

            var rootInnerExpression = TranslationContext.GetUltimateSource(innerExpression);
            var (collectionName, _) = rootInnerExpression.GetCollectionInfoFromQueryable(containerExpression);
            var innerTranslation = ExpressionToPipelineTranslator.Translate(context, innerExpression);
            var filterPipeline = innerTranslation.Ast.Stages.Count > 0 ? innerTranslation.Ast : null;
            return (collectionName, innerTranslation.OutputSerializer, filterPipeline);
        }

        // Builds the $lookup stage that joins the inner sequence into the "_inner" field. When the inner subquery
        // contains a cardinality operator the stage combines the correlation with the sub-pipeline in a way that
        // materializes the inner sequence globally, before the join. The pipeline-carrying forms require MongoDB
        // 5.0+ (Feature.LookupConciseSyntax); the bare form is supported by all servers.
        public static AstStage CreateLookupStage(
            string innerCollectionName, string localField, string foreignField, AstPipeline innerFilterPipeline)
        {
            if (innerFilterPipeline == null)
            {
                return AstStage.Lookup(from: innerCollectionName, localField, foreignField, @as: "_inner");
            }

            if (IsMembershipPreserving(innerFilterPipeline))
            {
                // The inner subquery only filters, reorders or reshapes rows (e.g. Where/OrderBy/Select), so the
                // localField/foreignField correlation may precede the sub-pipeline. This form lets the server use
                // an index on the foreign field.
                return AstStage.Lookup(innerCollectionName, localField, foreignField, [], innerFilterPipeline, "_inner");
            }

            // The inner subquery contains a cardinality operator (e.g. Take/Skip/Distinct) whose result must be
            // materialized globally, before the join. The localField/foreignField correlation runs before the
            // sub-pipeline, so using it here would apply that operator once per outer row. Instead we run the inner
            // sub-pipeline uncorrelated and apply the join equality as a trailing $match, ensuring the cardinality
            // operator sees the full inner sequence rather than a per-outer-key subset.
            var let = new[] { AstExpression.ComputedField("outer_key", AstExpression.FieldPath("$" + localField)) };
            var correlationFilter = AstFilter.Expr(
                AstExpression.Eq(AstExpression.FieldPath("$" + foreignField), AstExpression.Var("outer_key")));
            var correlatedInnerPipeline = new AstPipeline(innerFilterPipeline.Stages.Append(AstStage.Match(correlationFilter)));
            return AstStage.Lookup(innerCollectionName, let, correlatedInnerPipeline, "_inner");
        }

        private static bool IsMembershipPreserving(AstPipeline pipeline)
        {
            return pipeline.Stages.All(stage => __membershipPreservingStages.Contains(stage.NodeType));
        }
    }
}
