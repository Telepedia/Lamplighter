module Lamplighter.Shared.Layout

open Giraffe.ViewEngine

// Simple tracker of which route we are currently on and therefore which button should be highlighted
// in the navigation
type NavItem =
    | Dashboard
    | Edits
    | Config

// Icons are from Lucide and licensed under the ISC licence
let private icon (paths: string) =
    rawText $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">{paths}</svg>"""

let private dashboardIcon =
    icon """<rect width="7" height="9" x="3" y="3" rx="1"/><rect width="7" height="5" x="14" y="3" rx="1"/><rect width="7" height="9" x="14" y="12" rx="1"/><rect width="7" height="5" x="3" y="16" rx="1"/>"""

let private editsIcon =
    icon """<path d="M3 12h.01"/><path d="M3 18h.01"/><path d="M3 6h.01"/><path d="M8 12h13"/><path d="M8 18h13"/><path d="M8 6h13"/>"""

let private configIcon =
    icon """<line x1="21" x2="14" y1="4" y2="4"/><line x1="10" x2="3" y1="4" y2="4"/><line x1="21" x2="12" y1="12" y2="12"/><line x1="8" x2="3" y1="12" y2="12"/><line x1="21" x2="16" y1="20" y2="20"/><line x1="12" x2="3" y1="20" y2="20"/><line x1="14" x2="14" y1="2" y2="6"/><line x1="8" x2="8" y1="10" y2="14"/><line x1="16" x2="16" y1="18" y2="22"/>"""

let private navLink (active: NavItem) (item: NavItem) (href: string) (label: string) (navIcon: XmlNode) =
    let isActive = item = active
    a [
        _href href
        _class (if isActive then "lamplighter-nav__link lamplighter-nav__link--active" else "lamplighter-nav__link")
        _title label
        attr "aria-label" label
        if isActive then attr "aria-current" "page"
    ] [ navIcon ]

let main (active: NavItem) (pageTitle: string) (content: XmlNode list) =
    html [ _lang "en" ] [
        head [] [
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1" ]
            title [] [ str pageTitle ]
            link [
                _rel "stylesheet"
                _type "text/css"
                _href "/main.css"
            ]
        ]
        body [] [
            nav [ _class "lamplighter-nav"; attr "aria-label" "Main" ] [
                a [ _href "/"; _class "lamplighter-nav__logo"; attr "aria-label" "Lamplighter home" ] [ str "LL" ]
                div [ _class "lamplighter-nav__links" ] [
                    navLink active Dashboard "/" "Dashboard" dashboardIcon
                    navLink active Edits "/edits" "Edits" editsIcon
                ]
                div [ _class "lamplighter-nav__links lamplighter-nav__links--bottom" ] [
                    navLink active Config "/config" "Config" configIcon
                ]
            ]

            main [] content
        ]
    ]
