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
    
let private ofDataReader (rd: IDataReader) : FlaggedEdit =
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
      ReviewStatus = ReviewStatus.parse (rd.ReadString "review_status") }

// Claim the oldest edit that is due for scoring, so no other worker picks it up; it returns None if there is nothing
// to score
// this is a horrible query but alas 'tis what we must do
let claimNext (maxAttempts: int) (createConnection: unit -> IDbConnection) : FlaggedEdit option =
    use conn = createConnection ()
    conn
    |> Db.newCommand """
        UPDATE edits
        SET claimed_at = strftime('%Y-%m-%dT%H:%M:%SZ', 'now')
        WHERE id = (
            SELECT id
            FROM edits
            WHERE claimed_at IS NULL
              AND (scoring_status = 'pending'
                   OR (scoring_status = 'failed' AND scoring_attempts < @max_attempts))
              AND (next_attempt_at IS NULL
                   OR next_attempt_at <= strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
            ORDER BY id
            LIMIT 1
        )
        RETURNING id, wiki, wiki_id, page, url, editor, summary, diff, received,
                  config_version_id, score, scoring_status, review_status
        """
    |> Db.setParams [ "max_attempts", SqlType.Int32 maxAttempts ]
    |> Db.querySingle ofDataReader
   
// Return the scoring config for a specific edit 
let scoringConfigFor (edit: FlaggedEdit) (createConnection: unit -> IDbConnection) : ScoringConfig option =
   use conn = createConnection ()
   let version =
       conn
       |> Db.newCommand """
            SELECT model, prompt_template, temperature FROM config_versions WHERE id = @id
        """
       |> Db.setParams [ "id", SqlType.Int64 edit.ConfigVersionId ]
       |> Db.querySingle (fun (rd: IDataReader) ->
           rd.ReadString "model", rd.ReadStringOption "prompt_template", rd.ReadDoubleOption "temperature")
       
   match version with
    | None -> None
    | Some (model, promptTemplate, temperature) ->
        let questions =
            conn
            |> Db.newCommand """
                SELECT id, text FROM config_questions WHERE version_id = @id ORDER BY position
            """
            |> Db.setParams [ "id", SqlType.Int64 edit.ConfigVersionId ]
            |> Db.query (fun (rd: IDataReader) -> { Id = rd.ReadInt64 "id"; Text = rd.ReadString "text" })
        
        Some {
            Model = model
            PromptTemplate = promptTemplate
            Temperature = temperature
            Questions = questions
        }

// Save the answers returned by the model to the database, and its overall score;
// we do in transaction to avoid half-score if there is an error
let saveResults (editId: int64) (answers: (int64 * float) list) (score: float) (createConnection: unit -> IDbConnection) =
    use conn = createConnection ()
    conn
    |> Db.batch (fun tran ->
        Db.newCommandForTransaction """
            INSERT INTO edit_answers (edit_id, question_id, probability)
            VALUES (@edit_id, @question_id, @probability)
            """ tran
        |> Db.execMany (
            answers
            |> List.map (fun (questionId, probability) -> [
                "edit_id", SqlType.Int64 editId
                "question_id", SqlType.Int64 questionId
                "probability", SqlType.Double probability
            ]))

        Db.newCommandForTransaction """
            UPDATE edits
            SET score = @score,
                scoring_status = 'scored',
                scoring_error = NULL,
                claimed_at = NULL,
                next_attempt_at = NULL
            WHERE id = @id
            """ tran
        |> Db.setParams [
            "score", SqlType.Double score
            "id", SqlType.Int64 editId
        ]
        |> Db.exec)

// Record a failed scoring attempt and schedule the retry
// we retry 1 minute after the first failure, then 5 after the second, then 30.
// we only allow 3 attempts and then we stop trying to score this edit
let recordFailure (editId: int64) (error: string) (createConnection: unit -> IDbConnection) =
    use conn = createConnection ()
    conn
    |> Db.newCommand """
        UPDATE edits
        SET scoring_status = 'failed',
            scoring_attempts = scoring_attempts + 1,
            scoring_error = @error,
            claimed_at = NULL,
            next_attempt_at = strftime('%Y-%m-%dT%H:%M:%SZ', 'now',
                CASE scoring_attempts
                    WHEN 0 THEN '+1 minutes'
                    WHEN 1 THEN '+5 minutes'
                    ELSE '+30 minutes'
                END)
        WHERE id = @id
        """
    |> Db.setParams [
        "error", SqlType.String error
        "id", SqlType.Int64 editId
    ]
    |> Db.exec
