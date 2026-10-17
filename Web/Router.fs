module Lamplighter.Web.Router

open Giraffe

let routes : HttpHandler = 

    choose [
        Lamplighter.Home.Routes.routes
        Lamplighter.Edits.Routes.routes
        RequestErrors.NOT_FOUND "Page Not Found"
    ]