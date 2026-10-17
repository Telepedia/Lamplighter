module Lamplighter.Shared.Layout

open Giraffe.ViewEngine

let main (pageTitle: string) (content: XmlNode list) =
    html [] [
        head [] [
            title [] [ str pageTitle ]
            link [
                _rel "stylesheet"
                _type "text/css"
                _href "/main.css"
            ]
        ]
        body [] [
            header [] [
                div [ _class "lamplighter-header" ] [
                    div [ _class "lamplighter-header__content" ] [
                        h1 [ _class "lamplighter-logo" ] [ str "Lamplighter" ]
                    ]
                ]
            ]

            main [] content
        ]
    ]
