module Lamplighter.Edits.Types

type ScoringStatus =
    | Pending
    | Scored
    | Failed

module ScoringStatus =
    let parse = function
        | "pending" -> Pending
        | "scored" -> Scored
        | "failed" -> Failed
        | other -> failwith $"Unknown scoring status '{other}'"

    let toDb = function
        | Pending -> "pending"
        | Scored -> "scored"
        | Failed -> "failed"
        
// these types we reserve for human action 
type ReviewStatus =
    | Unreviewed
    | Approved
    | Reverted

module ReviewStatus =
    let parse = function
        | "unreviewed" -> Unreviewed
        | "approved" -> Approved
        | "reverted" -> Reverted
        | other -> failwith $"Unknown review status '{other}'"

    let toDb = function
        | Unreviewed -> "unreviewed"
        | Approved -> "approved"
        | Reverted -> "reverted"
        
type FlaggedEdit = {
    Id: int64
    Wiki: string
    WikiId: string
    Page: string
    Url: string
    Editor: string
    Summary: string
    Diff: string
    Received: string // MediaWiki is weird with timestamps, maybe we'll convert to DateTime later
    ConfigVersionId: int64
    Score: float option
    ScoringStatus: ScoringStatus
    ReviewStatus: ReviewStatus
}

// Model for an incoming edit from MediaWiki
type IncomingEdit = {
    Wiki: string
    WikiId: string
    Page: string
    Url: string
    Editor: string
    Summary: string
    Timestamp: string
    Diff: string
}

// this just combines the question (taken from the config version the edit was scored against) and the
// answer from the model
type QuestionAnswer = {
    QuestionId: int64
    Text: string
    Probability: float option
}
