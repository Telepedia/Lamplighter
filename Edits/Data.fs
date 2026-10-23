module Lamplighter.Edits.Data

open System.Data
open Donald
open Lamplighter.Edits.Types

let listEdits (createConnection: unit -> IDbConnection) : FlaggedEdit list =
    use conn = createConnection ()
    conn
    |> Db.newCommand """
        SELECT id, wiki, wiki_id, page, url, editor, summary, diff, received,
               config_version_id, score, scoring_status, review_status
        FROM edits
        ORDER BY id DESC
        LIMIT 50
        """
    |> Db.query (fun (rd: IDataReader) ->
        { Id = rd.ReadInt64 "id"
          Wiki = rd.ReadString "wiki"
          WikiId = rd.ReadString "wiki_id"
          Page = rd.ReadString "page"
          Url = rd.ReadString "url"
          Editor = rd.ReadString "editor"
          Summary = rd.ReadString "summary"
          Diff = rd.ReadString "diff"
          Received = rd.ReadString "received"
          ConfigVersionId = rd.ReadInt64 "config_version_id"
          Score = rd.ReadDoubleOption "score"
          ScoringStatus = ScoringStatus.parse (rd.ReadString "scoring_status")
          ReviewStatus = ReviewStatus.parse (rd.ReadString "review_status") })

// Load an edit from the database based on its ID
let fromId (id: int64) (createConnection: unit -> IDbConnection) =
    use conn = createConnection()
    conn
    |> Db.newCommand """
        SELECT * FROM edits WHERE id = @id
    """
    |> Db.setParams [
        "id", SqlType.Int64 id
    ]
    |> Db.querySingle (fun rd ->
        { Id = rd.ReadInt64 "id"
          Wiki = rd.ReadString "wiki"
          WikiId = rd.ReadString "wiki_id"
          Page = rd.ReadString "page"
          Url = rd.ReadString "url"
          Editor = rd.ReadString "editor"
          Summary = rd.ReadString "summary"
          Diff = rd.ReadString "diff"
          Received = rd.ReadString "received"
          ConfigVersionId = rd.ReadInt64 "config_version_id"
          Score = rd.ReadDoubleOption "score"
          ScoringStatus = ScoringStatus.parse (rd.ReadString "scoring_status")
          ReviewStatus = ReviewStatus.parse (rd.ReadString "review_status") })
    
// Load the questions from the config that the edit was scored against, including the answers if the
// model has scored and given its answers
let answersFor (edit: FlaggedEdit) (createConnection: unit -> IDbConnection) : QuestionAnswer list =
    use conn = createConnection ()
    conn
    |> Db.newCommand """
        SELECT q.id, q.text, a.probability
        FROM config_questions q
        LEFT JOIN edit_answers a ON a.question_id = q.id AND a.edit_id = @edit_id
        WHERE q.version_id = @version_id
        ORDER BY q.position
        """
    |> Db.setParams [
        "edit_id", SqlType.Int64 edit.Id
        "version_id", SqlType.Int64 edit.ConfigVersionId
    ]
    |> Db.query (fun (rd: IDataReader) ->
        { QuestionId = rd.ReadInt64 "id"
          Text = rd.ReadString "text"
          Probability = rd.ReadDoubleOption "probability" })

// Simple model of the config that the edit be scored against
let modelFor (edit: FlaggedEdit) (createConnection: unit -> IDbConnection) : string =
    use conn = createConnection ()
    conn
    |> Db.newCommand "SELECT model FROM config_versions WHERE id = @id"
    |> Db.setParams [ "id", SqlType.Int64 edit.ConfigVersionId ]
    |> Db.querySingle (fun (rd: IDataReader) -> rd.ReadString "model")
    |> Option.defaultValue "Unknown"

// release any scoring jobs that were claimed but have not ran for whatever reason, if they
// have been claimed for more than 10 minutes
let releaseStaleClaims (createConnection: unit -> IDbConnection) =
    use conn = createConnection ()
    conn
    |> Db.newCommand """
        UPDATE edits SET claimed_at = NULL WHERE claimed_at < strftime('%Y-%m-%dT%H:%M:%SZ', 'now', '-10 minutes')
    """
    |> Db.exec