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

using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions;

namespace MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToAggregationExpressionTranslators.PropertyTranslators
{
    internal static class LengthPropertyToAggregationExpressionTranslator
    {
        public static TranslatedExpression Translate(TranslationContext context, MemberExpression expression)
        {
            // note: array.Length is not handled here but in ArrayLengthExpressionToAggregationExpressionTranslator

            if (IsStringLengthProperty(expression))
            {
                var stringExpression = expression.Expression;
                var stringTranslation = ExpressionToAggregationExpressionTranslator.Translate(context, stringExpression);
                var ast = AstExpression.StrLenCP(stringTranslation.Ast);
                return new TranslatedExpression(expression, ast, new Int32Serializer());
            }

            throw new ExpressionNotSupportedException(expression);
        }

        private static bool IsStringLengthProperty(MemberExpression expression)
        {
            return
                expression.Member is PropertyInfo propertyInfo &&
                propertyInfo.DeclaringType == typeof(string) &&
                propertyInfo.PropertyType == typeof(int) &&
                propertyInfo.Name == "Length";
        }
    }
}
