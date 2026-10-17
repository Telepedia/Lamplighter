module Lamplighter.Home.Handlers

open Giraffe
open Lamplighter.Home.Views

let index : HttpHandler =
    htmlView homeView
