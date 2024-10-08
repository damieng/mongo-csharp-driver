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

using MongoDB.Driver.Authentication;
using MongoDB.Driver.Encryption;

namespace MongoDB.Driver
{
    /// <summary>
    /// Extension Manager provides a way to configure extensions for the driver.
    /// </summary>
    public interface IExtensionManager
    {
        /// <summary>
        /// Sasl Mechanisms Registry.
        /// </summary>
        ISaslMechanismRegistry SaslMechanisms { get; }

        /// <summary>
        /// Kms Providers Registry.
        /// </summary>
        IKmsProviderRegistry KmsProviders { get; }

        /// <summary>
        /// AutoEncryption Provider Registry.
        /// </summary>
        IAutoEncryptionProviderRegistry AutoEncryptionProvider { get; }
    }
}
