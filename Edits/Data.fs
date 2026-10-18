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