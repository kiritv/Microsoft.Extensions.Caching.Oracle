# Microsoft.Extensions.Caching.Oracle
## Distributed cache implementation of Microsoft.Extensions.Caching.Distributed.IDistributedCache using Oracle.

```sh
services.AddDistributedOracleCache(options => {
    options.ConnectionString = "Oracle database connection string";
    options.SchemaName = "Oracle schema name where Schema.plsql executed and SESSION_CACHE table and SESSION_CACHE_PKG exists";
 });
 ```