module Lamplighter.Config.Data

open System
open System.Data
open Donald
open Lamplighter.Config.Types

let private optionalString value =
    match value with
    | Some (s: string) -> SqlType.String s
    | None -> SqlType.Null

let private optionalFloat value =
    match value with
    | Some (f: float) -> SqlType.Double f
    | None -> SqlType.Null

// List all versions we know about
let getVersions (createConnection: unit -> IDbConnection) : ConfigVersion list =
    use conn = createConnection ()

    let questions =
        conn
        |> Db.newCommand "SELECT version_id, text FROM config_questions ORDER BY version_id, position"
        |> Db.query (fun (rd: IDataReader) -> rd.ReadInt64 "version_id", rd.ReadString "text")
        |> List.groupBy fst
        |> Map.ofList

    conn
    |> Db.newCommand """
        SELECT id, model, prompt_template, temperature, note, is_active, created_at, created_by
        FROM config_versions
        ORDER BY id DESC
        """
    |> Db.query (fun (rd: IDataReader) ->
        let id = rd.ReadInt64 "id"
        { Id = id
          Model = rd.ReadString "model"
          PromptTemplate = rd.ReadStringOption "prompt_template"
          Temperature = rd.ReadDoubleOption "temperature"
          Note = rd.ReadString "note"
          IsActive = rd.ReadInt64 "is_active" = 1L
          CreatedAt = rd.ReadString "created_at"
          CreatedBy = rd.ReadStringOption "created_by"
          Questions =
            questions
            |> Map.tryFind id
            |> Option.map (List.map snd)
            |> Option.defaultValue [] })

let private activateInTransaction (id: int64) (tran: IDbTransaction) =
    // we have to clear the existing one first or else the constraint fails
    Db.newCommandForTransaction "UPDATE config_versions SET is_active = 0 WHERE is_active = 1" tran
    |> Db.exec

    Db.newCommandForTransaction "UPDATE config_versions SET is_active = 1 WHERE id = @id" tran
    |> Db.setParams [ "id", SqlType.Int64 id ]
    |> Db.exec

// Saves a new version with its questions and marks it as active
let createVersion (createConnection: unit -> IDbConnection) (version: NewConfigVersion) : int64 =
    use conn = createConnection ()
    conn
    |> Db.batch (fun tran ->
        let id =
            Db.newCommandForTransaction """
                INSERT INTO config_versions (model, prompt_template, temperature, note)
                VALUES (@model, @prompt_template, @temperature, @note)
                RETURNING id
                """ tran
            |> Db.setParams [
                "model", SqlType.String version.Model
                "prompt_template", optionalString version.PromptTemplate
                "temperature", optionalFloat version.Temperature
                "note", SqlType.String version.Note
            ]
            |> Db.scalar Convert.ToInt64

        Db.newCommandForTransaction "INSERT INTO config_questions (version_id, position, text) VALUES (@version_id, @position, @text)" tran
        |> Db.execMany (
            version.Questions
            |> List.mapi (fun position text -> [
                "version_id", SqlType.Int64 id
                "position", SqlType.Int32 position
                "text", SqlType.String text
            ]))

        activateInTransaction id tran
        id)

// Reverts an existing config set to be the active one
let activateVersion (createConnection: unit -> IDbConnection) (id: int64) : bool =
    use conn = createConnection ()

    let exists =
        conn
        |> Db.newCommand "SELECT COUNT(*) FROM config_versions WHERE id = @id"
        |> Db.setParams [ "id", SqlType.Int64 id ]
        |> Db.scalar Convert.ToInt64
        |> (<) 0L

    if exists then
        conn |> Db.batch (activateInTransaction id)
    exists

let getThreshold (createConnection: unit -> IDbConnection) : float option =
    use conn = createConnection ()
    conn
    |> Db.newCommand "SELECT value FROM settings WHERE key = 'threshold'"
    |> Db.querySingle (fun (rd: IDataReader) -> rd.ReadString "value")
    |> Option.bind (fun value ->
        match Double.TryParse(value, Globalization.CultureInfo.InvariantCulture) with
        | true, threshold -> Some threshold
        | _ -> None)

let setThreshold (createConnection: unit -> IDbConnection) (threshold: float) =
    use conn = createConnection ()
    conn
    |> Db.newCommand """
        INSERT INTO settings (key, value) VALUES ('threshold', @value)
        ON CONFLICT (key) DO UPDATE SET value = excluded.value
        """
    |> Db.setParams [ "value", SqlType.String (threshold.ToString(Globalization.CultureInfo.InvariantCulture)) ]
    |> Db.exec
