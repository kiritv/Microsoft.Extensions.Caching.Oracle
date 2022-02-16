// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Oracle;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for setting up Microsoft Oracle distributed cache services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class OracleCachingServicesExtensions
    {
        /// <summary>
        /// Adds Microsoft Oracle distributed caching services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="setupAction">An <see cref="Action{OracleCacheOptions}"/> to configure the provided <see cref="OracleCacheOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddDistributedOracleCache(this IServiceCollection services, Action<OracleCacheOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddOptions();
            AddOracleCacheServices(services);
            services.Configure(setupAction);

            return services;
        }

        // to enable unit testing
        internal static void AddOracleCacheServices(IServiceCollection services)
        {
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, OracleCache>());
        }
    }
}