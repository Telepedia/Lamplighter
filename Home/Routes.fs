module Lamplighter.Home.Routes

open Giraffe

let routes : HttpHandler =
    choose [
        GET >=> route "/" >=> Handlers.index
    ]