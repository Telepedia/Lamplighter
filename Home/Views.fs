module Lamplighter.Home.Views

open Lamplighter.Shared
open Giraffe.ViewEngine

let homeView=
    Layout.main Layout.Dashboard "Lamplighter" [
        section [ _class "lamplighter-overview" ] [
            h2 [] [ str "Welcome to Lamplighter 🔥" ]
            p [ _class "lamplighter-overview__description" ] [
                str "Lamplighter is an automated tool which flags questionable content found in MediaWiki edits; the tool is meant to increase the efficiency of platform trust and safety. Lamplighter does not take any action against edits itself; it is merely a tool to score the probability of which an edit should be reverted, and edits which are not flagged are not stored in Lamplighter. Please use this tool responsibly."
            ]
        ]
    ]