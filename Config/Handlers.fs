module Lamplighter.Config.Handlers

open System
open System.Data
open System.Globalization
open Microsoft.AspNetCore.Http
open Giraffe
open Lamplighter.Config.Types
open Lamplighter.Config.Views

let private connection (ctx: HttpContext) =
    ctx.GetService<unit -> IDbConnection>()

let private tryParseFloat (value: string) =
    match Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture) with
    | true, res -> Some res
    | _ -> None

let configPage : HttpHandler =
    fun next ctx ->
        let db = connection ctx
        htmlView (configView (Data.getVersions db) (Data.getThreshold db) None) next ctx

// we use the existing config as the base for a new version so it can just be tweaked
let newVersionPage : HttpHandler =
    fun next ctx ->
        let active = Data.getVersions (connection ctx) |> List.tryFind _.IsActive
        let values =
            match active with
            | Some version ->
                { Model = version.Model
                  PromptTemplate = version.PromptTemplate |> Option.defaultValue ""
                  Temperature = version.Temperature |> Option.map string |> Option.defaultValue ""
                  Note = ""
                  Questions = String.Join("\n", version.Questions) }
            | None ->
                { Model = List.head availableModels; PromptTemplate = ""; Temperature = ""; Note = ""; Questions = "" }
        htmlView (newVersionView values []) next ctx

let private validate (values: VersionForm) : Result<NewConfigVersion, string list> =
    let questions =
        values.Questions.Split('\n')
        |> Array.map _.Trim()
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> List.ofArray

    let temperature =
        if String.IsNullOrWhiteSpace values.Temperature then Ok None
        else
            match tryParseFloat values.Temperature with
            | Some t when t >= 0.0 && t <= 2.0 -> Ok (Some t)
            | _ -> Error "Temperature must be between 0 and 2."

    let errors = [
        if not (List.contains values.Model availableModels) then "Pick a model from the list."
        if List.isEmpty questions then "Add at least one question."
        match temperature with
        | Error e -> e
        | Ok _ -> ()
    ]

    match errors, temperature with
    | [], Ok temperature ->
        Ok { Model = values.Model
             PromptTemplate = if String.IsNullOrWhiteSpace values.PromptTemplate then None else Some (values.PromptTemplate.Trim())
             Temperature = temperature
             Note = values.Note.Trim()
             Questions = questions }
    | errors, _ -> Error errors

let createVersion : HttpHandler =
    fun next ctx ->
        task {
            let! form = ctx.Request.ReadFormAsync()
            let values =
                { Model = string form["model"]
                  PromptTemplate = string form["prompt_template"]
                  Temperature = string form["temperature"]
                  Note = string form["note"]
                  Questions = string form["questions"] }

            match validate values with
            | Error errors ->
                return! (setStatusCode 400 >=> htmlView (newVersionView values errors)) next ctx
            | Ok version ->
                Data.createVersion (connection ctx) version |> ignore
                return! redirectTo false "/config" next ctx
        }

let activateVersion (id: int64) : HttpHandler =
    fun next ctx ->
        if Data.activateVersion (connection ctx) id then
            redirectTo false "/config" next ctx
        else
            RequestErrors.NOT_FOUND "No such configuration version" next ctx

let saveThreshold : HttpHandler =
    fun next ctx ->
        task {
            let! form = ctx.Request.ReadFormAsync()
            let db = connection ctx

            match tryParseFloat (string form["threshold"]) with
            | Some threshold when threshold >= 0.0 && threshold <= 1.0 ->
                Data.setThreshold db threshold
                return! redirectTo false "/config" next ctx
            | _ ->
                let view = configView (Data.getVersions db) (Data.getThreshold db) (Some "Threshold must be between 0 and 1.")
                return! (setStatusCode 400 >=> htmlView view) next ctx
        }
