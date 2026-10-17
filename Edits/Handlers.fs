module Lamplighter.Edits.Handlers

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Giraffe
open Lamplighter.Edits.Types

// This is just a no-op here until we have some backend storage wired up
let private saveEdit (edit: IncomingEdit) : Task<unit> =
    Task.FromResult ()

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
                do! saveEdit edit
                return! Successful.ACCEPTED "Ingested" next ctx
        }
