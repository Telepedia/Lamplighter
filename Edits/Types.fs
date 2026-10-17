module Lamplighter.Edits.Types

type ReviewStatus =
    | Pending
    | Approved
    | Reverted

type FlaggedEdit = {
    Id: int
    Wiki: string
    WikiId: string
    Page: string
    Editor: string
    Summary: string
    Diff: string
    Url: string
    Received: string // MediaWiki is weird with timestamps, maybe we'll convert to DateTime later
    Score: float
    Status: ReviewStatus
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
