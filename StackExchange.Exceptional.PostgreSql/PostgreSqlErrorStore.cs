using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Npgsql;
using StackExchange.Exceptional.Extensions;

namespace StackExchange.Exceptional.PostgreSql
{
    public class PostgreSqlErrorStore : ErrorStore
    {
        #region Constants

        /// <summary>
        ///     The maximum count of errors to show.
        /// </summary>
        public const int MaximumDisplayCount = 500;

        /// <summary>
        ///     The default maximum count of errors shown at once.
        /// </summary>
        public const int DefaultDisplayCount = 200;

        #endregion Constants

        #region Fields

        private readonly string _connectionString;
        private readonly int _displayCount;

        #endregion Fields

        #region Constructors

        /// <summary>
        ///     Creates a new instance of <see cref="PostgreSqlErrorStore" /> with the given configuration.
        /// </summary>
        public PostgreSqlErrorStore(ErrorStoreSettings settings)
            : base(settings)
        {
            this._displayCount = Math.Min(settings.Size, PostgreSqlErrorStore.MaximumDisplayCount);

            this._connectionString = settings.ConnectionString.IsNullOrEmpty()
                ? ErrorStore.GetConnectionStringByName(settings.ConnectionStringName)
                : settings.ConnectionString;

            if (this._connectionString.IsNullOrEmpty())
                throw new ArgumentOutOfRangeException(nameof(settings), "A connection string or connection string name must be specified when using a SQL error store");
        }

        /// <summary>
        ///     Creates a new instance of <see cref="PostgreSqlErrorStore" /> with the specified connection string.
        /// </summary>
        /// <param name="connectionString">The database connection string to use</param>
        /// <param name="displayCount">
        ///     How many errors to display in the log (for display ONLY, the log is not truncated to this
        ///     value)
        /// </param>
        /// <param name="rollupSeconds">
        ///     The rollup seconds, defaults to <see cref="ErrorStore.DefaultRollupSeconds" />, duplicate
        ///     errors within this time period will be rolled up
        /// </param>
        public PostgreSqlErrorStore(string connectionString, int displayCount = PostgreSqlErrorStore.DefaultDisplayCount, int rollupSeconds = ErrorStore.DefaultRollupSeconds)
            : base(rollupSeconds)
        {
            this._displayCount = Math.Min(displayCount, PostgreSqlErrorStore.MaximumDisplayCount);

            if (connectionString.IsNullOrEmpty())
                throw new ArgumentOutOfRangeException(nameof(connectionString), "Connection string must be specified when using a SQL error store");
            this._connectionString = connectionString;
        }

        #endregion Constructors

        #region Overriden Members

        /// <summary>
        ///     Name for this error store
        /// </summary>
        public override string Name => "PostgreSql Error Store";

        /// <summary>
        ///     Deleted all errors in the log, by setting DeletionDate to UTC Now (DateTime.UtcNow)
        /// </summary>
        /// <returns>True if any errors were deleted, false otherwise</returns>
        protected override bool DeleteAllErrors(string applicationName = null)
        {
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                return dbConnection.Execute(Sql.DeleteAllErrors, new
                {
                    ApplicationName = applicationName.IsNullOrEmptyReturn(ErrorStore.ApplicationName),
                    DeletionDate = DateTime.UtcNow
                }) > 0;
            }
        }

        /// <summary>
        ///     Deletes an error, by setting DeletionDate to UTC Now (DateTime.UtcNow)
        /// </summary>
        /// <param name="guid">The guid of the error to delete</param>
        /// <returns>True if the error was found and deleted, false otherwise</returns>
        protected override bool DeleteError(Guid guid)
        {
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                return dbConnection.Execute(Sql.DeleteError, new
                {
                    GUID = guid,
                    DeletionDate = DateTime.UtcNow
                }) > 0;
            }
        }

        /// <summary>
        ///     Retrieves all non-deleted application errors in the database
        /// </summary>
        protected override int GetAllErrors(List<Error> errors, string applicationName = null)
        {
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                errors.AddRange(dbConnection.Query<Error>(Sql.GetAllErrors, new
                {
                    Max = this._displayCount,
                    ApplicationName = applicationName.IsNullOrEmptyReturn(ErrorStore.ApplicationName)
                }));
            }

            return errors.Count;
        }

        /// <summary>
        ///     Gets the error with the specified guid from SQL
        ///     This can return a deleted error as well, there's no filter based on DeletionDate
        /// </summary>
        /// <param name="guid">The guid of the error to retrieve</param>
        /// <returns>The error object if found, null otherwise</returns>
        protected override Error GetError(Guid guid)
        {
            Error sqlError;
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                // a guid won't collide, but the AppName is for security
                sqlError = dbConnection.Query<Error>(Sql.GetError, new
                {
                    GUID = guid,
                }).FirstOrDefault();
            }
            if (sqlError == null)
                return null;

            // everything is in the JSON, but not the columns and we have to deserialize for collections anyway
            // so use that deserialized version and just get the properties that might change on the SQL side and apply them
            var result = Error.FromJson(sqlError.FullJson);
            result.DuplicateCount = sqlError.DuplicateCount;
            result.DeletionDate = sqlError.DeletionDate;
            return result;
        }

        /// <summary>
        ///     Retrieves a count of application errors since the specified date, or all time if null
        /// </summary>
        protected override int GetErrorCount(DateTime? since = null, string applicationName = null)
        {
            var query = Sql.GetErrorCount + (since.HasValue ? " AND creation_date > :Since" : string.Empty);
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                return dbConnection.Query<int>(query, new
                {
                    Since = since,
                    ApplicationName = applicationName.IsNullOrEmptyReturn(ErrorStore.ApplicationName)
                }).FirstOrDefault();
            }
        }

        /// <summary>
        ///     Logs the error to SQL
        ///     If the rollup conditions are met, then the matching error will have a DuplicateCount += @DuplicateCount (usually 1,
        ///     unless in retry) rather than a distinct new row for the error
        /// </summary>
        /// <param name="error">The error to log</param>
        protected override void LogError(Error error)
        {
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                if (this.RollupThreshold.HasValue && error.ErrorHash.HasValue)
                {
                    var count = dbConnection.Execute(Sql.CountExceptions, new
                    {
                        DuplicateCount = error.DuplicateCount,
                        ErrorHash = error.ErrorHash,
                        ApplicationName = error.ApplicationName.Truncate(50),
                        MinDate = DateTime.UtcNow.Add(this.RollupThreshold.Value.Negate())
                    });

                    // if we found an exception that's a duplicate, jump out
                    if (count > 0)
                    {
                        error.GUID = dbConnection.Query<Guid>(Sql.GetExceptionGuid, new
                        {
                            ErrorHash = error.ErrorHash,
                            ApplicationName = error.ApplicationName.Truncate(50),
                            MinDate = DateTime.UtcNow.Add(this.RollupThreshold.Value.Negate())
                        }).First();
                        return;
                    }
                }

                error.FullJson = error.ToJson();

                dbConnection.Execute(Sql.Insert, new
                {
                    GUID = error.GUID,
                    ApplicationName = error.ApplicationName.Truncate(50),
                    MachineName = error.MachineName.Truncate(50),
                    CreationDate = error.CreationDate,
                    Type = error.Type.Truncate(100),
                    IsProtected = error.IsProtected,
                    Host = error.Host.Truncate(100),
                    Url = error.Url.Truncate(500),
                    HTTPMethod = error.HTTPMethod.Truncate(10), // this feels silly, but you never know when someone will up and go crazy with HTTP 1.2!
                    IPAddress = error.IPAddress,
                    Source = error.Source.Truncate(100),
                    Message = error.Message.Truncate(1000),
                    Detail = error.Detail,
                    StatusCode = error.StatusCode,
                    Sql = (string.IsNullOrEmpty(error.SQL) ? "none" : error.SQL),
                    FullJson = error.FullJson,
                    ErrorHash = error.ErrorHash,
                    DuplicateCount = error.DuplicateCount,
                });
            }
        }

        /// <summary>
        ///     Protects an error from deletion, by making IsProtected = 1 in the database
        /// </summary>
        /// <param name="guid">The guid of the error to protect</param>
        /// <returns>True if the error was found and protected, false otherwise</returns>
        protected override bool ProtectError(Guid guid)
        {
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                return dbConnection.Execute(Sql.ProtectError, new
                {
                    GUID = guid
                }) > 0;
            }
        }

        /// <summary>
        ///     Protects errors from deletion, by making IsProtected = 1 in the database
        /// </summary>
        /// <param name="guids">The guids of the error to protect</param>
        /// <returns>True if the errors were found and protected, false otherwise</returns>
        protected override bool ProtectErrors(IEnumerable<Guid> guids)
        {
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                return dbConnection.Execute(Sql.ProtectErrors, new
                {
                    GUIDs = guids
                }) > 0;
            }
        }

        /// <summary>
        ///     Deletes errors, by setting DeletionDate to UTC Now (DateTime.UtcNow)
        /// </summary>
        /// <param name="guids">The guids of the error to delete</param>
        /// <returns>True if the errors were found and deleted, false otherwise</returns>
        protected override bool DeleteErrors(IEnumerable<Guid> guids)
        {
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                return dbConnection.Execute(Sql.DeleteErrors, new
                {
                    GUIDs = guids,
                    DeletionDate = DateTime.UtcNow
                }) > 0;
            }
        }

        /// <summary>
        ///     Hard deletes an error, actually deletes the row from SQL rather than setting DeletionDate
        ///     This is used to cleanup when testing the error store when attempting to come out of retry/failover mode after
        ///     losing connection to SQL
        /// </summary>
        /// <param name="guid">The guid of the error to hard delete</param>
        /// <returns>True if the error was found and deleted, false otherwise</returns>
        protected override bool HardDeleteError(Guid guid)
        {
            using (var dbConnection = new NpgsqlConnection(this._connectionString))
            {
                return dbConnection.Execute(Sql.HardDeleteError, new
                {
                    GUID = guid,
                    ApplicationName = ErrorStore.ApplicationName
                }) > 0;
            }
        }

        #endregion Overriden Members
    }
}