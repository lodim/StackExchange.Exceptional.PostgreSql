namespace StackExchange.Exceptional.PostgreSql
{
    public static class Sql
    {
        public const string ProtectError = @"
UPDATE public.""Errors""
   SET ""IsProtected"" = true, ""DeletionDate"" = NULL
 WHERE ""GUID"" = :GUID
";

        public const string ProtectErrors = @"
UPDATE public.""Errors""
   SET ""IsProtected"" = true, ""DeletionDate"" = NULL
 WHERE ""GUID"" IN :GUIDs
";

        public const string DeleteError = @"
UPDATE public.""Errors""
   SET ""DeletionDate"" = :DeletionDate 
 WHERE ""GUID"" = :GUID 
   AND ""DeletionDate"" IS NULL
";

        public const string DeleteErrors = @"
UPDATE public.""Errors""
   SET ""DeletionDate"" = :DeletionDate 
 WHERE ""GUID"" IN :GUIDs 
   AND ""DeletionDate"" IS NULL
";

        public const string HardDeleteError = @"
DELETE FROM public.""Errors""
 WHERE ""GUID"" = :GUID
   AND ""ApplicationName"" = :ApplicationName
";

        public const string DeleteAllErrors = @"
UPDATE public.""Errors""
   SET ""DeletionDate"" = :DeletionDate 
 WHERE ""DeletionDate"" IS NULL 
   AND ""IsProtected"" = false
   AND ""ApplicationName"" = :ApplicationName
";

        public const string Insert = @"
INSERT INTO public.""Errors""(""GUID"", ""ApplicationName"", ""MachineName"", ""CreationDate"", ""Type"", ""IsProtected"", ""Host"", ""Url"", ""HTTPMethod"", ""IPAddress"", ""Source"", ""Message"", ""Detail"", ""StatusCode"", ""SQL"", ""FullJson"", ""ErrorHash"", ""DuplicateCount"")
VALUES (:GUID, :ApplicationName, :MachineName, :CreationDate, :Type, :IsProtected, :Host, :Url, :HTTPMethod, :IPAddress, :Source, :Message, :Detail, :StatusCode, :Sql, :FullJson, :ErrorHash, :DuplicateCount)
";

        public const string CountExceptions = @"
UPDATE public.""Errors""
   SET ""DuplicateCount"" = ""DuplicateCount"" + :DuplicateCount
 WHERE ""ErrorHash"" = :ErrorHash
   AND ""ApplicationName"" = :ApplicationName
   AND ""DeletionDate"" IS NULL
   AND ""CreationDate"" >= :MinDate 
";

        public const string GetExceptionGuid = @"
SELECT ""GUID"" FROM public.""Errors""
 WHERE ""ErrorHash"" = :ErrorHash 
   AND ""ApplicationName"" = :ApplicationName
   AND ""DeletionDate"" IS NULL
   AND ""CreationDate"" >= :MinDate 
 LIMIT 1 
";

        public const string GetError = @"
SELECT * 
  FROM public.""Errors""
 WHERE ""GUID"" = :GUID
";

        public const string GetAllErrors = @"
SELECT * 
  FROM public.""Errors""
 WHERE ""DeletionDate"" IS NULL
   AND ""ApplicationName"" = :ApplicationName
 ORDER BY ""CreationDate"" DESC 
 LIMIT :Max
";

        public const string GetErrorCount = @"
SELECT COUNT(*) 
  FROM public.""Errors""
 WHERE ""DeletionDate"" IS NULL
   AND ""ApplicationName"" = :ApplicationName
";
    }
}