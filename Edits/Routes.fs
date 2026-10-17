module Lamplighter.Edits.Routes

open Giraffe

let routes : HttpHandler =
    choose [
        POST >=> route "/ingest" >=> Handlers.ingestEditFromMediaWiki
    ]
