module Lamplighter.Config.Types

// Models that can be picked in the interface
// @TODO: we probably need to move these into config somehow, appsettings? idk but i'll deal with that
// at a later stage
let availableModels = [ "Jev"; "Microsoft-Decision 1" ]

// A saved configuration entry from the DB
type ConfigVersion = {
    Id: int64
    Model: string
    PromptTemplate: string option
    Temperature: float option
    Note: string
    IsActive: bool
    CreatedAt: string
    CreatedBy: string option
    Questions: string list
}

// Model for new config submission
type NewConfigVersion = {
    Model: string
    PromptTemplate: string option
    Temperature: float option
    Note: string
    Questions: string list
}