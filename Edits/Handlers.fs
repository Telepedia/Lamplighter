module Lamplighter.Edits.Handlers

open System.Data
open System.Data.Common
open System.Threading.Tasks
open Donald
open Lamplighter.Home
open Microsoft.AspNetCore.Http
open Giraffe
open Lamplighter.Edits.Types

// Save an edit to the database; it doesn't run any scoring, that is a separate process
let private saveEdit (createConnection: unit -> IDbConnection) (edit: IncomingEdit) : Task<int64 option> =
    task {
        use conn = createConnection ()
        return!
            conn
            |> Db.newCommand """
            INSERT INTO edits (wiki, wiki_id, page, url, editor, diff, received, summary, config_version_id)
            SELECT @wiki, @wiki_id, @page, @url, @editor, @diff, @received, @summary, id
            FROM config_versions
            WHERE is_active = 1
            RETURNING id
            """
            |> Db.setParams [
                "wiki", SqlType.String edit.Wiki
                "wiki_id", SqlType.String edit.WikiId
                "page", SqlType.String edit.Page
                "url", SqlType.String edit.Url
                "editor", SqlType.String edit.Editor
                "diff", SqlType.String edit.Diff
                "received", SqlType.String edit.Timestamp
                "summary", SqlType.String (edit.Summary |> Option.ofObj |> Option.defaultValue "")
            ]
            |> Db.Async.querySingle (fun (rd: DbDataReader) -> rd.ReadInt64 "id")
    }

// Check whether we have everything required to save to the database;
// we force the edit summary since MediaWiki doesn't require and not everyone will provide
let private isValid (edit: IncomingEdit) =
    not (isNull (box edit))
    && [ edit.Wiki; edit.WikiId; edit.Page; edit.Editor; edit.Timestamp; edit.Diff ]
       |> List.forall (System.String.IsNullOrWhiteSpace >> not)

// Ingest an edit from MediaWiki; that is, save it to the database
// Note: this doesn't do any scoring etc, it just saves it
let ingestEditFromMediaWiki : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let! edit =
                task {
                    try
                        let! edit = ctx.BindJsonAsync<IncomingEdit>()
                        return if isValid edit then Some edit else None
                    with _ ->
                        return None
                }

            match edit with
            | None ->
                return! RequestErrors.BAD_REQUEST "Malformed edit payload" next ctx
            | Some edit ->
                match! saveEdit (ctx.GetService<unit -> IDbConnection>()) edit with
                | Some _ -> return! Successful.ACCEPTED "Ingested" next ctx
                | None -> return! ServerErrors.SERVICE_UNAVAILABLE "There is no active configuration. Please set up the Lamplighter config." next ctx
        }

let index : HttpHandler =
    fun next ctx ->
        let edits = Data.listEdits (ctx.GetService<unit -> IDbConnection>())
        htmlView (Views.indexView edits) next ctx