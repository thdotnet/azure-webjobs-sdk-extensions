﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBAttributeBindingProvider : IBindingProvider
    {
        private JobHostConfiguration _jobHostConfig;
        private DocumentDBConfiguration _docDBConfig;
        private TraceWriter _trace;

        public DocumentDBAttributeBindingProvider(JobHostConfiguration config, DocumentDBConfiguration documentDBConfig, TraceWriter trace)
        {
            _jobHostConfig = config;
            _docDBConfig = documentDBConfig;
            _trace = trace;
        }

        public async Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            DocumentDBAttribute attribute = parameter.GetCustomAttribute<DocumentDBAttribute>(inherit: false);
            if (attribute == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(_docDBConfig.ConnectionString) &&
                string.IsNullOrEmpty(attribute.ConnectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The DocumentDB connection string must be set either via a '{0}' app setting, via the DocumentDBAttribute.ConnectionString property or via DocumentDBConfiguration.ConnectionString.",
                    DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName));
            }

            DocumentDBContext documentDBContext = CreateContext(_docDBConfig, attribute, _jobHostConfig.NameResolver, _trace);

            IBindingProvider compositeProvider = new CompositeBindingProvider(new IBindingProvider[]
            {
                new DocumentDBOutputBindingProvider(documentDBContext, _jobHostConfig.GetService<IConverterManager>()),
                new DocumentDBClientBinding(parameter, documentDBContext),
                new DocumentDBItemBinding(parameter, documentDBContext, context)
            });

            return await compositeProvider.TryCreateAsync(context);
        }

        internal static DocumentDBContext CreateContext(DocumentDBConfiguration config, DocumentDBAttribute attribute, INameResolver resolver, TraceWriter trace)
        {
            string resolvedConnectionString = config.ConnectionString;

            if (!string.IsNullOrEmpty(attribute.ConnectionString))
            {
                resolvedConnectionString = DocumentDBConfiguration.GetSettingFromConfigOrEnvironment(attribute.ConnectionString);
            }

            return new DocumentDBContext
            {
                Service = config.DocumentDBServiceFactory.CreateService(resolvedConnectionString),
                ResolvedDatabaseName = Resolve(attribute.DatabaseName, resolver),
                ResolvedCollectionName = Resolve(attribute.CollectionName, resolver),
                ResolvedId = attribute.Id,
                Trace = trace,
                ResolvedPartitionKey = attribute.PartitionKey,
                CreateIfNotExists = attribute.CreateIfNotExists,
                CollectionThroughput = attribute.CollectionThroughput
            };
        }

        private static string Resolve(string value, INameResolver resolver)
        {
            if (resolver == null)
            {
                return value;
            }

            return resolver.ResolveWholeString(value);
        }
    }
}