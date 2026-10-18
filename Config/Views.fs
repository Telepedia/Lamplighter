module Lamplighter.Config.Views

open Lamplighter.Shared
open Lamplighter.Config.Types
open Giraffe.ViewEngine

// Bit of a hack, but raw values so that if saving fails, we can re-show the form with the
// user supplied data
type VersionForm = {
    Model: string
    PromptTemplate: string
    Temperature: string
    Note: string
    Questions: string
}

let private optionalText (value: string option) =
    value |> Option.defaultValue "—"

let private errorList (errors: string list) =
    match errors with
    | [] -> []
    | errors ->
        [ ul [ _class "lamplighter-config__errors" ] (errors |> List.map (fun e -> li [] [ str e ])) ]

let private activeVersion (version: ConfigVersion option) =
    section [ _class "lamplighter-config__section" ] [
        h3 [] [ str "Active version" ]
        match version with
        | None ->
            p [] [ str "There is no active configuration. No edits will be scored until one is created." ]
        | Some version ->
            dl [ _class "lamplighter-config__details" ] [
                dt [] [ str "Version" ]
                dd [] [ str $"#{version.Id}" ]
                dt [] [ str "Model" ]
                dd [] [ str version.Model ]
                dt [] [ str "Temperature" ]
                dd [] [ str (version.Temperature |> Option.map string |> optionalText) ]
                dt [] [ str "Prompt template" ]
                dd [] [ pre [] [ str (optionalText version.PromptTemplate) ] ]
                dt [] [ str "Note" ]
                dd [] [ str version.Note ]
                dt [] [ str "Created" ]
                dd [] [ str version.CreatedAt ]
            ]
            h4 [] [ str "Questions" ]
            ol [ _class "lamplighter-config__questions" ] (version.Questions |> List.map (fun q -> li [] [ str q ]))
        a [ _href "/config/new" ] [
            button [] [ str "Create a new version" ]
        ]
    ]

let private thresholdForm (threshold: float option) (error: string option) =
    section [ _class "lamplighter-config__section" ] [
        h3 [] [ str "Flag threshold" ]
        p [] [ str "Edits scoring at or above this are flagged. Altering the threshold applies to edits which have been scored but no action has yet been taken." ]
        yield! errorList (Option.toList error)
        form [ _method "post"; _action "/config/threshold"; _class "lamplighter-config__form" ] [
            label [ _for "threshold" ] [ str "Threshold (0 to 1)" ]
            input [
                _type "number"; _id "threshold"; _name "threshold"
                _min "0"; _max "1"; _step "0.01"; _required
                _value (threshold |> Option.map string |> Option.defaultValue "")
            ]
            button [ _type "submit" ] [ str "Save threshold" ]
        ]
    ]

let private versionRow (version: ConfigVersion) =
    tr [] [
        td [] [ str $"#{version.Id}" ]
        td [] [ str version.CreatedAt ]
        td [] [ str version.Model ]
        td [] [
            details [] [
                summary [] [ str $"{version.Questions.Length} questions" ]
                ol [] (version.Questions |> List.map (fun q -> li [] [ str q ]))
            ]
        ]
        td [] [ str version.Note ]
        td [] [
            if version.IsActive then
                str "Active"
            else
                form [ _method "post"; _action $"/config/versions/{version.Id}/activate" ] [
                    button [ _type "submit" ] [ str "Make active" ]
                ]
        ]
    ]

let private history (versions: ConfigVersion list) =
    section [ _class "lamplighter-config__section" ] [
        h3 [] [ str "Version history" ]
        match versions with
        | [] -> p [] [ str "No versions yet." ]
        | versions ->
            table [ _class "lamplighter-config__history" ] [
                thead [] [
                    tr [] [
                        th [] [ str "Version" ]
                        th [] [ str "Created" ]
                        th [] [ str "Model" ]
                        th [] [ str "Questions" ]
                        th [] [ str "Note" ]
                        th [] []
                    ]
                ]
                tbody [] (versions |> List.map versionRow)
            ]
    ]

let configView (versions: ConfigVersion list) (threshold: float option) (thresholdError: string option) =
    Layout.main "Configuration – Lamplighter" [
        section [ _class "lamplighter-config" ] [
            h2 [] [ str "Configuration" ]
            p [ _class "lamplighter-config__description" ] [
                str "Each edit is scored against the configuration that was active at the time scoring started for that edit. Changing these values therefore has no impact on edits already being scored."
            ]
            activeVersion (versions |> List.tryFind _.IsActive)
            thresholdForm threshold thresholdError
            history versions
        ]
    ]

let newVersionView (values: VersionForm) (errors: string list) =
    Layout.main "New configuration – Lamplighter" [
        section [ _class "lamplighter-config" ] [
            h2 [] [ str "New configuration version" ]
            p [ _class "lamplighter-config__description" ] [
                str "Saving creates a new version and marks it as active. History is kept and can be reverted to at any time"
            ]
            yield! errorList errors
            form [ _method "post"; _action "/config"; _class "lamplighter-config__form" ] [
                label [ _for "model" ] [ str "Model" ]
                select [ _id "model"; _name "model"; _required ] [
                    for model in availableModels do
                        option [ _value model; if model = values.Model then _selected ] [ str model ]
                ]

                label [ _for "temperature" ] [ str "Temperature (optional, 0 to 2)" ]
                input [ _type "number"; _id "temperature"; _name "temperature"; _min "0"; _max "2"; _step "0.1"; _value values.Temperature ]

                label [ _for "prompt_template" ] [ str "Prompt template (optional)" ]
                textarea [ _id "prompt_template"; _name "prompt_template"; _rows "4" ] [ str values.PromptTemplate ]

                label [ _for "questions" ] [ str "Questions, one per line" ]
                textarea [ _id "questions"; _name "questions"; _rows "8"; _required ] [ str values.Questions ]

                label [ _for "note" ] [ str "Summary of changes" ]
                input [ _type "text"; _id "note"; _name "note"; _value values.Note ]

                div [ _class "lamplighter-config__actions" ] [
                    button [ _type "submit" ] [ str "Save and mark active" ]
                    a [ _href "/config" ] [ str "Cancel" ]
                ]
            ]
        ]
    ]