// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using System.Threading;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Collections.Generic;

namespace Microsoft.Extensions.Caching.Oracle
{
    internal class OracleDatabaseOperations : IDatabaseOperations
    {
        protected string ConnectionString { get; }
        protected string SchemaName { get; }
        protected ISystemClock SystemClock { get; }

        public OracleDatabaseOperations(string connectionString, string schemaName, ISystemClock systemClock)
        {
            ConnectionString = connectionString;
            SchemaName = schemaName;
            SystemClock = systemClock;
        }

        private async Task<byte[]> ExecuteProcedureAsync(string procedureName, List<OracleParameter> parameters = null, byte[] value = null)
        {
            byte[] result = null;
            using (var oracleConnection = new OracleConnection(ConnectionString))
            {
                await oracleConnection.OpenAsync();
                var oracleTransaction = await oracleConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                try
                {
                    using (var oracleCommand = new OracleCommand(procedureName, oracleConnection))
                    {
                        oracleCommand.BindByName = true;
                        oracleCommand.CommandType = CommandType.StoredProcedure;
                        if (parameters != null)
                        {
                            if (value != null)
                            {
                                var blob = new OracleBlob(oracleConnection);
                                blob.Write(value, 0, (int)value.Length);
                                oracleCommand.Parameters.Add(new OracleParameter { ParameterName = "p_value", OracleDbType = OracleDbType.Blob, Value = blob });
                            }
                            foreach (var parameter in parameters)
                            {
                                oracleCommand.Parameters.Add(parameter);
                            }
                        }
                        await oracleCommand.ExecuteNonQueryAsync();

                        if (parameters != null)
                        {
                            foreach (var parameter in parameters)
                            {
                                if ((parameter.OracleDbType == OracleDbType.Blob) && (parameter.Direction == ParameterDirection.Output))
                                {
                                    OracleBlob blob_data = (OracleBlob)parameter.Value;
                                    if (!blob_data.IsNull)
                                    {
                                        result = blob_data.Value;
                                    }
                                }
                            }
                        }
                    }
                    await oracleTransaction.CommitAsync();
                    await oracleConnection.CloseAsync();
                }
                catch (Exception ex)
                {
                    await oracleTransaction.RollbackAsync();
                    await oracleConnection.CloseAsync();
                    throw new Exception(ex.Message);
                }
            }
            return result;
        }
        private byte[] ExecuteProcedure(string procedureName, List<OracleParameter> parameters = null, byte[] value = null)
        {
            byte[] result = null;
            using (var oracleConnection = new OracleConnection(ConnectionString))
            {
                oracleConnection.Open();
                var oracleTransaction = oracleConnection.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    using (var oracleCommand = new OracleCommand(procedureName, oracleConnection))
                    {
                        oracleCommand.BindByName = true;
                        oracleCommand.CommandType = CommandType.StoredProcedure;
                        if (parameters != null)
                        {
                            if (value != null)
                            {
                                var blob = new OracleBlob(oracleConnection);
                                blob.Write(value, 0, (int)value.Length);
                                oracleCommand.Parameters.Add(new OracleParameter { ParameterName = "p_value", OracleDbType = OracleDbType.Blob, Value = blob });
                            }
                            foreach (var parameter in parameters)
                            {
                                oracleCommand.Parameters.Add(parameter);
                            }
                        }
                        oracleCommand.ExecuteNonQuery();

                        if (parameters != null)
                        {
                            foreach (var parameter in parameters)
                            {
                                if ((parameter.OracleDbType == OracleDbType.Blob) && (parameter.Direction == ParameterDirection.Output))
                                {
                                    OracleBlob blob_data = (OracleBlob)parameter.Value;
                                    if (!blob_data.IsNull)
                                    {
                                        result = blob_data.Value;
                                    }
                                }
                            }
                        }
                    }
                    oracleTransaction.Commit();
                    oracleConnection.Close();
                }
                catch (Exception ex)
                {
                    oracleTransaction.Rollback();
                    oracleConnection.Close();
                    throw new Exception(ex.Message);
                }
            }
            return result;
        }
        public void DeleteCacheItem(string key)
        {
            List<OracleParameter> parameters = new List<OracleParameter>();
            parameters.Add(new OracleParameter { ParameterName = "p_key", OracleDbType = OracleDbType.Varchar2, Value = key });
            var procedureName = $"{SchemaName}.SESSION_CACHE_PKG.Delete_Cache";
            ExecuteProcedure(procedureName, parameters);
        }
        public byte[] GetCacheItem(string key)
        {
            List<OracleParameter> parameters = new List<OracleParameter>();
            parameters.Add(new OracleParameter { ParameterName = "p_key", OracleDbType = OracleDbType.Varchar2, Value = key });
            parameters.Add(new OracleParameter { ParameterName = "p_value", OracleDbType = OracleDbType.Blob, Value = key, Direction = ParameterDirection.Output });
            var procedureName = $"{SchemaName}.SESSION_CACHE_PKG.Get_Cache";
            return ExecuteProcedure(procedureName, parameters);
        }
        public void RefreshCacheItem(string key)
        {
            GetCacheItem(key);
        }
        public virtual void DeleteExpiredCacheItems()
        {
            var procedureName = $"{SchemaName}.SESSION_CACHE_PKG.DeleteExpiredCache";
            ExecuteProcedure(procedureName);
        }
        public virtual void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            var absoluteExpiration = GetAbsoluteExpiration(SystemClock.UtcNow, options);
            ValidateOptions(options.SlidingExpiration, absoluteExpiration);

            List<OracleParameter> parameters = new List<OracleParameter>();
            parameters.Add(new OracleParameter { ParameterName = "p_key", OracleDbType = OracleDbType.Varchar2, Value = key });
            parameters.Add(new OracleParameter { ParameterName = "p_slidingExpirationInSeconds", OracleDbType = OracleDbType.Int64, Value = options.SlidingExpiration?.TotalSeconds });
            parameters.Add(new OracleParameter { ParameterName = "p_absoluteExpiration", OracleDbType = OracleDbType.TimeStamp, Value = (absoluteExpiration ?? (object)DBNull.Value) });
            var procedureName = $"{SchemaName}.SESSION_CACHE_PKG.Put_Cache";
            ExecuteProcedure(procedureName, parameters, value);
        }
        protected DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset utcNow, DistributedCacheEntryOptions options)
        {
            // calculate absolute expiration
            DateTimeOffset? absoluteExpiration = null;
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
            else if (options.AbsoluteExpiration.HasValue)
            {
                if (options.AbsoluteExpiration.Value <= utcNow)
                {
                    throw new InvalidOperationException("The absolute expiration value must be in the future.");
                }
                absoluteExpiration = options.AbsoluteExpiration.Value;
            }
            return absoluteExpiration;
        }
        protected void ValidateOptions(TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
        {
            if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
            {
                throw new InvalidOperationException("Either absolute or sliding expiration needs " +
                    "to be provided.");
            }
        }
        public async Task<byte[]> GetCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            List<OracleParameter> parameters = new List<OracleParameter>();
            parameters.Add(new OracleParameter { ParameterName = "p_key", OracleDbType = OracleDbType.Varchar2, Value = key });
            parameters.Add(new OracleParameter { ParameterName = "p_value", OracleDbType = OracleDbType.Blob, Value = key, Direction = ParameterDirection.Output });
            var procedureName = $"{SchemaName}.SESSION_CACHE_PKG.Get_Cache";
            return await ExecuteProcedureAsync(procedureName, parameters);
        }
        public async Task RefreshCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            await GetCacheItemAsync(key, token: token);
        }
        public async Task DeleteCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            List<OracleParameter> parameters = new List<OracleParameter>();
            parameters.Add(new OracleParameter { ParameterName = "p_key", OracleDbType = OracleDbType.Varchar2, Value = key });
            var procedureName = $"{SchemaName}.SESSION_CACHE_PKG.Delete_Cache";
            await ExecuteProcedureAsync(procedureName, parameters);
        }
        public async Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            var absoluteExpiration = GetAbsoluteExpiration(SystemClock.UtcNow, options);
            ValidateOptions(options.SlidingExpiration, absoluteExpiration);

            List<OracleParameter> parameters = new List<OracleParameter>();
            parameters.Add(new OracleParameter { ParameterName = "p_key", OracleDbType = OracleDbType.Varchar2, Value = key });
            parameters.Add(new OracleParameter { ParameterName = "p_slidingExpirationInSeconds", OracleDbType = OracleDbType.Int64, Value = options.SlidingExpiration?.TotalSeconds });
            parameters.Add(new OracleParameter { ParameterName = "p_absoluteExpiration", OracleDbType = OracleDbType.TimeStamp, Value = (absoluteExpiration ?? (object)DBNull.Value) });
            var procedureName = $"{SchemaName}.SESSION_CACHE_PKG.Put_Cache";
            await ExecuteProcedureAsync(procedureName, parameters, value);
        }
    }
}