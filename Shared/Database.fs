module Lamplighter.Shared.Database

open System.Data
open Microsoft.Data.Sqlite

// Create a connection to the SQL database – the caller is responsible for disposing of
// it once they have finished with it
let createConnection (connectionString: string) () : IDbConnection =
    new SqliteConnection(connectionString)