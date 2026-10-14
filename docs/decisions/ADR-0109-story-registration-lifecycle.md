# ADR-0109 — Story registration and campaign lifecycle

**Status:** Accepted (Tech lead, 2026-10-14).

## Context

The app creates one canonical LogicContext before the services that consume it. ADR-0103 requires DialogPresenter to subscribe before BeatService so Ended closes the screen before terminal effects. App-lifetime services are constructed before a campaign has a seed or generated galaxy. A seed captured only at construction would incorrectly use the boot seed for every campaign.

## Implementation default

Preserve every existing public constructor and bootstrap/bridge signature. NarrativeBootstrap.Build still validates/binds all owners and creates the canonical facts/context/service. An internal BuildStory phase, called after Screens.BindDialog, constructs the actual QuestService, ClueService and BeatService, binds NarrativeLogicAdapter, registers their ordered systems, and constructs ShipLogQuery and QuestSaveParticipant. App registers those owners and QUST before starting any session, binds the existing HUD objective hook, and disposes the query before beats. The presentation worker owns ship-log screen binding.

Build the authored-hash to canonical StarSystemId and template-hash to StoryTag dictionaries once from catalog definitions, without requiring a generated galaxy. Refresh BeatService's private seed from context.CampaignSeed in OnNewGame and OnRestored. GameSession assigns the new seed before BeginNewGame; SaveService restores SESS before GameSession calls NarrativeService.OnSessionRestored. New games clear queues/per-body state and reset transient completion state; restore retains QUST-owned queues/per-body state and applies no effects.

An internal production bridge factory binds real travel/encounter/beat/UI/dialog owners and a cached Hulk/PersistentId query. Travel subscribes first so accepted lane interactions are already Spooling. Explicit interactions read current modal/dialog/travel state at delivery, allowing Screens and combat, and exclude Salvage/Hulk targets before fact writes or beat dispatch. The presentation tick separately excludes Screens, absent/Hulk player and engaging hostiles for automatic beats.

## Consequences

Accepted lifecycle overload: NarrativeService.BeginNewGame(bool runStory), with the existing no-argument method retaining true. GameSession passes !sandbox; canonical state still resets, but sandbox ticks cannot launch campaign story. Ordinary and creative campaigns retain story; restored campaigns enable evaluation as before. Live integration exposed the intro pausing sandbox startup, so the existing playground entry/reset case also verifies no dialog. No package, assembly-reference, shared data contract, RNG algorithm or state-writer change. Exactly the five required QST-008 functional tests remain; they use real app owners, compiled GraphDialogRunner and the actual navigation/presentation bridge. Source checks do not replace the lead's Unity compile and functional run.
