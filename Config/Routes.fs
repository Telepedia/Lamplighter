module Lamplighter.Config.Routes

open Giraffe

let routes : HttpHandler =
    choose [
        GET >=> choose [
            route "/config" >=> Handlers.configPage
            route "/config/new" >=> Handlers.newVersionPage
        ]
        POST >=> choose [
            route "/config" >=> Handlers.createVersion
            route "/config/threshold" >=> Handlers.saveThreshold
            routef "/config/versions/%d/activate" Handlers.activateVersion
        ]
    ]