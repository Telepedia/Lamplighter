module Lamplighter.Edits.Views

open System
open System.Globalization
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
    
let private percent (probability: float) =
    sprintf "%.0f%%" (probability * 100.0)

let private isFlagged (edit: FlaggedEdit) (threshold: float option) =
    match edit.ScoringStatus, edit.Score, threshold with
    | Scored, Some score, Some threshold -> score >= threshold
    | _ -> false

// a makeshift gauge 
let private gauge (score: float option) (flagged: bool) =
    let arc = "M 10 60 A 50 50 0 0 1 110 60"
    let fill =
        match score with
        | Some score ->
            let modifier = if flagged then " lamplighter-gauge__fill--flagged" else ""
            $"""<path class="lamplighter-gauge__fill{modifier}" d="{arc}" pathLength="100" stroke-dasharray="{sprintf "%.1f" (score * 100.0)} 100"/>"""
        | None -> ""
    let value = score |> Option.map percent |> Option.defaultValue "—"
    rawText $"""<svg class="lamplighter-gauge" viewBox="0 0 120 68" role="img" aria-label="Overall score {value}"><path class="lamplighter-gauge__track" d="{arc}"/>{fill}<text class="lamplighter-gauge__value" x="60" y="58" text-anchor="middle">{value}</text></svg>"""

// Summary of the score and where its up to
let private scoreSummary (edit: FlaggedEdit) (threshold: float option) =
    let thresholdText = threshold |> Option.map percent |> Option.defaultValue "not set"
    match edit.ScoringStatus with
    | Pending -> "Not scored yet."
    | Failed -> "Scoring failed; it will be retried."
    | Scored when isFlagged edit threshold -> $"Flagged: at or above the {thresholdText} threshold."
    | Scored -> $"Below the {thresholdText} threshold."

let private detail (label: string) (value: XmlNode) =
    div [ _class "lamplighter-edit__detail" ] [
        dt [] [ str label ]
        dd [] [ value ]
    ]

let private answerRow (highest: int64 option) (answer: QuestionAnswer) =
    li [ _class (if Some answer.QuestionId = highest then "lamplighter-edit__answer lamplighter-edit__answer--highest" else "lamplighter-edit__answer") ] [
        span [ _class "lamplighter-edit__question" ] [ str answer.Text ]
        span [ _class "lamplighter-edit__bar" ] [
            match answer.Probability with
            | Some probability -> span [ _style (sprintf "width: %.1f%%" (probability * 100.0)) ] []
            | None -> ()
        ]
        span [ _class "lamplighter-edit__probability" ] [
            str (answer.Probability |> Option.map percent |> Option.defaultValue "—")
        ]
    ]

// MediaWiki diffs with - and + for removed and added, so we check for those and
// colour the lines accordingly
let private diffLine (line: string) =
    let modifier =
        if line.StartsWith "+" then " lamplighter-edit__diff-line--added"
        elif line.StartsWith "-" then " lamplighter-edit__diff-line--removed"
        else ""
    span [ _class $"lamplighter-edit__diff-line{modifier}" ] [ str line ]

let editView (edit: FlaggedEdit) (model: string) (answers: QuestionAnswer list) (threshold: float option) =
    let flagged = isFlagged edit threshold
    // find out which of the questions had the highest score and therefore caused
    // the edit to be flagged
    let highest =
        answers
        |> List.filter _.Probability.IsSome
        |> function
            | [] -> None
            | scored -> Some (scored |> List.maxBy _.Probability).QuestionId

    Layout.main Layout.Edits $"Review {edit.Page} – Lamplighter" [
        section [ _class "lamplighter-edit" ] [
            h2 [] [ str $"{edit.Page} – {edit.Wiki}" ]

            div [ _class "lamplighter-edit__block lamplighter-edit__overview" ] [
                dl [ _class "lamplighter-edit__details" ] [
                    detail "Wiki" (str edit.Wiki)
                    detail "Page" (a [ _href edit.Url; _target "_blank"; _rel "noopener" ] [ str $"{edit.Page} (view diff on wiki)" ])
                    detail "Editor" (str edit.Editor)
                    detail "Summary" (str (if edit.Summary = "" then "(no summary)" else edit.Summary))
                    detail "Received (UTC)" (str (timeToHuman edit.Received))
                    detail "Configuration" (a [ _href "/config" ] [ str $"Version #{edit.ConfigVersionId}, {model}" ])
                    detail "Scoring" (str (ScoringStatus.toDb edit.ScoringStatus))
                    detail "Review" (str (ReviewStatus.toDb edit.ReviewStatus))
                ]
                div [ _class "lamplighter-edit__score" ] [
                    span [ _class "lamplighter-edit__score-label" ] [ str "Overall score" ]
                    gauge edit.Score flagged
                    p [ _class "lamplighter-edit__score-summary" ] [ str (scoreSummary edit threshold) ]
                ]
            ]

            div [ _class "lamplighter-edit__block" ] [
                h3 [] [ str "Diff" ]
                pre [ _class "lamplighter-edit__diff" ] (
                    edit.Diff.Split('\n') |> Array.map diffLine |> List.ofArray
                )
            ]

            div [ _class "lamplighter-edit__block" ] [
                h3 [] [ str "Questions" ]
                p [ _class "lamplighter-edit__note" ] [
                    str "The probability the model gave of the answer being yes. The overall score is the highest of them."
                ]
                ol [ _class "lamplighter-edit__answers" ] (answers |> List.map (answerRow highest))
            ]
        ]
    ]
