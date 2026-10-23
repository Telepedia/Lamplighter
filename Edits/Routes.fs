module Lamplighter.Edits.Routes

open Giraffe

let routes : HttpHandler =
    choose [
        GET >=> route "/edits" >=> Handlers.index
        GET >=> routef "/edits/%d" Handlers.individualEdit
        POST >=> route "/ingest" >=> Handlers.ingestEditFromMediaWiki
    ]
