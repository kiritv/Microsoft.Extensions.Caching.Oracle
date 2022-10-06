// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Oracle.ManagedDataAccess.Client;

namespace Microsoft.Extensions.Caching.Oracle
{
    public interface IOracleDatabaseOperations
    {
        Task<byte[]> ExecuteProcedureAsync(string info, string procedureName, List<OracleParameter> parameters = null, byte[] value = null);
        byte[] ExecuteProcedure(string info, string procedureName, List<OracleParameter> parameters = null, byte[] value = null);

    }
    public interface IDatabaseOperations
    {
        byte[] GetCacheItem(string key);
        Task<byte[]> GetCacheItemAsync(string key, CancellationToken token = default(CancellationToken));

        void RefreshCacheItem(string key);
        Task RefreshCacheItemAsync(string key, CancellationToken token = default(CancellationToken));

        void DeleteCacheItem(string key);
        Task DeleteCacheItemAsync(string key, CancellationToken token = default(CancellationToken));

        void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options);
        Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken));

        void DeleteExpiredCacheItems();
    }
}