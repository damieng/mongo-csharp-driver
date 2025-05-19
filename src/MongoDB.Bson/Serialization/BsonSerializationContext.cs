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

using System;
using MongoDB.Bson.IO;

namespace MongoDB.Bson.Serialization
{
    /// <summary>
    /// Represents all the contextual information needed by a serializer to serialize a value.
    /// </summary>
    public class BsonSerializationContext
    {
        // constructors
        private BsonSerializationContext(
            IBsonWriter writer,
            Func<Type, bool> isDynamicType)
        {
            Writer = writer;
            IsDynamicType = isDynamicType;
        }

        // public properties
        /// <summary>
        /// Gets a function that, when executed, will indicate whether the type
        /// is a dynamic type.
        /// </summary>
        public Func<Type, bool> IsDynamicType { get; }

        /// <summary>
        /// Gets the writer.
        /// </summary>
        /// <value>
        /// The writer.
        /// </value>
        public IBsonWriter Writer { get; }

        /// <summary>
        ///
        /// </summary>
        public bool SanitizeBsonValues { get; set; }

        // public static methods
        /// <summary>
        /// Creates a root context.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="configurator">The serialization context configurator.</param>
        /// <returns>
        /// A root context.
        /// </returns>
        public static BsonSerializationContext CreateRoot(
            IBsonWriter writer,
            Action<Builder> configurator = null)
        {
            var builder = new Builder(null, writer);
            if (configurator != null)
            {
                configurator(builder);
            }
            return builder.Build();
        }

        /// <summary>
        /// Creates a new context with some values changed.
        /// </summary>
        /// <param name="configurator">The serialization context configurator.</param>
        /// <returns>
        /// A new context.
        /// </returns>
        public BsonSerializationContext With(
            Action<Builder> configurator = null)
        {
            var builder = new Builder(this, Writer);
            if (configurator != null)
            {
                configurator(builder);
            }
            return builder.Build();
        }

        // nested classes
        /// <summary>
        /// Represents a builder for a BsonSerializationContext.
        /// </summary>
        public class Builder
        {
            // constructors
            internal Builder(BsonSerializationContext other, IBsonWriter writer)
            {
                if (writer == null)
                {
                    throw new ArgumentNullException("writer");
                }

                Writer = writer;
                if (other != null)
                {
                    IsDynamicType = other.IsDynamicType;
                }
                else
                {
                    IsDynamicType = t =>
                        (BsonDefaults.DynamicArraySerializer != null && t == BsonDefaults.DynamicArraySerializer.ValueType) ||
                        (BsonDefaults.DynamicDocumentSerializer != null && t == BsonDefaults.DynamicDocumentSerializer.ValueType);
                }
            }

            // properties
            /// <summary>
            /// Gets or sets the function used to determine if a type is a dynamic type.
            /// </summary>
            public Func<Type, bool> IsDynamicType { get; set; }

            /// <summary>
            /// Gets the writer.
            /// </summary>
            /// <value>
            /// The writer.
            /// </value>
            public IBsonWriter Writer { get; }

            // public methods
            /// <summary>
            /// Builds the BsonSerializationContext instance.
            /// </summary>
            /// <returns>A BsonSerializationContext.</returns>
            internal BsonSerializationContext Build()
            {
                return new BsonSerializationContext(Writer, IsDynamicType);
            }
        }
    }
}
