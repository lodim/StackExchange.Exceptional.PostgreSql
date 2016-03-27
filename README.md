# StackExchange.Exceptional.PostgreSql

StackExchange.Exceptional is the error handler used internally by [Stack Exchange](http://stackexchange.com) and [Stack Overflow](http://stackoverflow.com) for logging to SQL. The original work of Nick Craver has support for SQL Server, MySQL JSON and memory error stores, and this package adds support for **PostgreSql** as well.

### Installation

Using nuget:

```sh
PM> Install-Package StackExchange.Exceptional.PostgreSql
```

Afterwards don't forget to run the [sql script](https://raw.githubusercontent.com/MihaiBogdanEugen/StackExchange.Exceptional.PostgreSql/master/StackExchange.Exceptional.PostgreSql/DbScripts/PostgreSqlErrors.sql) for creating the database infrastructure - just as in the original package.

[See the wiki for how to get configured and logging in just a few minutes](https://github.com/NickCraver/StackExchange.Exceptional/wiki).

This project is licensed under the [Apache 2.0 license](http://www.apache.org/licenses/LICENSE-2.0).