module Lamplighter.Edits.Views

open System
open System.Globalization
open Giraffe
open Lamplighter.Shared
open Lamplighter.Edits.Types
open Giraffe.ViewEngine

let private scoreText (score: float option) =
    score |> Option.map (sprintf "%.2f") |> Option.defaultValue "—"

let private timeToHuman (time: string) =
    let t = DateTime.ParseExact(
        time,
        "yyyyMMddHHmmss",
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal ||| DateTimeStyles.AdjustToUniversal
    )
    
    t.ToString("dd MMM yyyy, HH:mm")
    
let private editRow (edit: FlaggedEdit) =
    tr [] [
        td [] [ str (timeToHuman edit.Received) ]
        td [] [ str edit.Wiki ]
        td [] [ a [ _href $"/edits/{edit.Id}" ] [ str edit.Page ] ]
        td [] [ str edit.Editor ]
        td [] [ str (scoreText edit.Score) ]
        td [] [ str (ScoringStatus.toDb edit.ScoringStatus) ]
        td [] [ str (ReviewStatus.toDb edit.ReviewStatus) ]
    ]

let indexView (edits: FlaggedEdit list) =
    Layout.main Layout.Edits "Edits – Lamplighter" [
        section [ _class "lamplighter-edits" ] [
            h2 [] [ str "Edits" ]
            match edits with
            | [] -> p [] [ str "No edits have been received from MediaWiki yet." ]
            | edits ->
                table [ _class "lamplighter-edits__table" ] [
                    thead [] [
                        tr [] [
                            th [] [ str "Received (UTC)" ]
                            th [] [ str "Wiki" ]
                            th [] [ str "Page" ]
                            th [] [ str "Editor" ]
                            th [] [ str "Score" ]
                            th [] [ str "Scoring" ]
                            th [] [ str "Review" ]
                        ]
                    ]
                    tbody [] (edits |> List.map editRow)
                ]
        ]
    ]