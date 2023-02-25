# baser
Database Tool


# Instructions
## Creating a Database
Open baser, and use the `New {colSize} {colCount} {path}` command to create a new Database. DB is created in a .dbr file at `path`. `colSize` indicates the number of bytes allocated to each column, and the `colCount` indicates number of columns per row. The maximum value for each `colSize` and `colCount` is `2^16`

## Hosting a remote database
On the remote host, follow the instructions for creating a database. Then, either open this database, and use the `enableapi {port}` command, where port is the port on which API requests will be handled, or run `./baser.exe {path} {apiPort}` (as Administrator on Windows), or `sudo dotnet baser.dll {path} {apiPort}` (on Linux), where `path` is the location of the database, and `apiPort` is the port on which API requests will be handled.

## Using a database
Refer to the `help` command available once a database is opened.

## Querying the database
You can run query commands either directly `query {query}` or by simply entering your query `{query}`.
Query options include
- `columnName.is=`, `columnName.has=`, `columnName.starts=` and `columnName.ends`, where `columnName` is the name of the desired column to filter (note that column names are taken to be the very first row of the database (i.e. row 0).
- You can search across all columns by not specifying a column name, i.e. simply `.is=`, `.has=`, `.starts=` and `.ends=`.
- You can perform not equal searches with ~ (i.e. `columnName.is~`)
- You can perform AND and OR operations, e.g. `query1&query2`, `query1%query2`, `query1&query2%query3`.
- Note that OR (`%`) operations are the primary split points. As in, `A&B%C`, the request will be split first at %, hence a search of `(A and B) or C` will be returned. For `(A and B) or (A and C)` a search of `A&B%A&C` is required.
