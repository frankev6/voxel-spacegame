# SPEC-42 — Story beats, quests, clues and the mystery

| | |
|---|---|
| **Status** | Draft |
| **Area code** | QST (task IDs are `QST-001`, `QST-002`, …) |
| **Module(s)** | `Game.Narrative` (folders `Assets/Game/Narrative/Quests/`, `Assets/Game/Narrative/Beats/`, `Assets/Game/Narrative/Clues/`, `Assets/Game/Narrative/ShipLog/`); `Game.Data` (`Assets/Game/Data/Definitions/Narrative/` for the definitions); `Game.App` (world-event routing and player-free gating, in SPEC-40's bridge files); `Game.Editor` (validator rules); content under `Assets/Game/Content/Narrative/` |
| **Depends on specs** | SPEC-40 (`FactStore`, `FactCatalog`, generated families, `INarrativeSystem`, `NarrativeService`, `ILogicNarrative`, `CompiledCondition`/`CompiledIntExpr`/`CompiledEffectList`, `LogicScope`, `LogicBodyHandle`, `DependencyIndex`, `INarrativeRuleProvider`, converter); SPEC-41 (`DialogService`, `DialogRequest`, `DialogEnded`, `DialogOutcome`, node lookup); SPEC-30 (`AuthoredStarSystemDefinition`, `BodyTemplateDefinition.storyTag`, authored star-system content); SPEC-31/32/33 (world events routed by the App bridge); SPEC-02 (`RngStreams`, `StableHash`, `ISaveParticipant`); SPEC-05 (`QUST` chunk) |
| **Depended on by** | SPEC-40 (`BeatService`, `QuestService`, `ClueService`, `NarrativeLogicAdapter`, converter targets §4.2–§4.5, placement §4.10, clue texts §4.11); SPEC-41 (`StoryBeatDefinition.yarnNode`, beat lifecycle); SPEC-61 (`ShipLogQuery`, markers, tracked objective); SPEC-30 GAL-012 (opening-chapter star systems §4.12); SPEC-33 (hails); SPEC-70 (quest probes); SPEC-05 (`QUST`) |
| **Milestone** | M3 |

> Read [SPEC-40](SPEC-40-narrative-core.md) (§3, §4.2–§4.5, §4.10, §5.3–§5.8) and [SPEC-41](SPEC-41-dialog.md) (§3, §4.7, §5.3) before this spec. This spec may not contradict them.

## 1. Purpose

This spec is the story's structure on top of SPEC-40's facts and logic and SPEC-41's dialog. It has four parts:
- **Story beats** (storylets) decide *what plays where*. When the player interacts with a body, hails a ship, enters a star system, starts a new game, or a condition becomes true, `BeatService` picks the highest-salience eligible beat for that trigger and plays its Yarn node. When the dialog ends, it counts the beat and applies its effects.
- **Quests** are declarative views over facts. Stages of objectives complete when their conditions hold, measured from baselines captured when the quest or stage begins. This fixes the old retroactive completion (05-B3), load-time completion (05-B8) and re-acceptance bugs (05-B7).
- **Clues** are revealed in discovery order and add up to **mysteries**. **Rumors** are clues that point at a place on the map.
- **Ship log and markers.** The ship log and quest markers give the UI one read model.

The spec also fixes where every converted legacy encounter plays (§4.10), the texts of converted clues and rumors (§4.11), and the opening chapter of the vertical slice (§4.12).

## 2. Scope

**In scope:**
- `StoryBeatDefinition`, `QuestDefinition`, `ClueDefinition`, `RumorDefinition`, `MysteryDefinition` (fields, compile/bind, validation).
- `BeatService` (triggers, selection, queue, dialog lifecycle, terminal commands, nested completion), `QuestService` (instances, stages, objectives, baselines, log, tracking), `ClueService` (reveal order, mysteries, rumor resolution).
- `NarrativeLogicAdapter` (`ILogicNarrative`), `ShipLogQuery` (read model), `NarrativeMarkers`.
- The `QUST` save chunk.
- Quest, beat and mystery validator rules (`QST-V*`).
- Legacy placement table, tag vocabulary, clue and rumor texts.
- Opening chapter: star-system list for SPEC-30, new writing, and a playthrough test.

**Out of scope (and who owns it instead):**
- Facts, logic language, converter mechanics: SPEC-40. Dialog runner and screen: SPEC-41.
- Bodies, star systems and their content assets: SPEC-30/31 (this spec gives them the list and the tags).
- Encounters spawned by beats: SPEC-33.
- Ship log and map screens: SPEC-61 (they read §6).
- Procedural contract generation (parameterised mission templates): not in v1 (§10 Q4). The three legacy templates become fixed repeatable contracts.

## 3. Decisions (binding)

1. **One beat plays at a time, and only through `DialogService`.**
   - A beat without a `yarnNode` is an effect-only beat. It completes immediately when selected.
   - Automatic beats (GameStart, StarSystemEntered, Condition, queued) start only when the player is free (§5.1).
   - Interact and Hail beats start immediately on the player's press if no dialog or modal is active.

   Reason: 05-B4/B5 softlocks; SPEC-41 decision 1.
2. **Selection** is the highest `salience` among eligible beats.
   - Ties are broken by a seeded weighted pick: `RngStreams.For(campaignSeed, RngPurpose.Narrative, StableHash.Combine(selectionSalt, contextKey))`, where `selectionSalt` = the current value of `sys_beats_completed` and `contextKey` = the body key (0 for non-body triggers).
   - **Eligible** means: trigger matches, location matches, repeat rule allows, and `condition` is true at selection time.

   Reason: deterministic replays of the same playthrough; hubs re-select each visit (audit 05 §5.6).
3. **Beat counts are generated facts** (SPEC-40 §4.2: the beat ID fact, written only by `BeatService`). A beat completes when its dialog ends with outcome `Completed` and no `abort_beat` ran, or when `complete_beat` names it. Completion writes `beat += 1` and `sys_beats_completed += 1`, records the per-body repeat key, then applies `onComplete`. Reason: quests and conditions read completions as facts.
4. **Quests never complete from the past.**
   - On accept, and on each stage entry, every fact slot read by that quest's conditions is captured as a baseline; `since_accept` and `since_stage` read these baselines (SPEC-40).
   - Objectives evaluate only when a dependency changes, or on a full evaluation after new game or load.
   - A full evaluation after load never applies effects (decision 6).
   - Reason: 05-B3, 05-B10.
5. **`start_quest` is idempotent.**
   - An active instance means no-op.
   - A completed or failed non-repeatable quest means no-op.
   - A repeatable quest (kind `Contract`) gets a new instance.
   - There is no abandon in v1.

   Reason: 05-B7, 05-B9.
6. **Loading never runs effects.**
   - `OnRestored` marks everything dirty.
   - On the first ready tick the systems re-evaluate:
     - objectives that became true are recorded **silently** (their `done` bit and stage progression are restored from the save, not re-derived);
     - only conditions that change *after* that tick produce effects.

   Reason: 05-B8, SPEC-40 decision 12.
7. **Clue ordinals.**
   - Revealing an unknown clue sets its fact to `++sys_clue_counter`.
   - A mystery is solved, once, when all its required clues are known; its `onSolved` effects then apply.
   - Rumors show a map marker until their `resolvedWhen` condition is true.

   Reason: discovery order is story data; the audit 05 §5.5 clue graph.
8. **Text lives in definitions, never in saves.**
   - Saves hold IDs, statuses, stage indices, done bits, baselines (by fact **name**), log entry IDs, the tracked quest, the per-body beat keys, the beat queue and seen clues.
   - Unknown IDs are preserved verbatim.

   Reason: 04-B1; content edits never corrupt saves.
9. **Narrative never references Galaxy types.**
   - Locations are authored star-system definitions and body **tags** (SPEC-30 `BodyTemplateDefinition.storyTag`).
   - Bodies arrive as SPEC-40 `LogicBodyHandle`.
   - The App bridge converts star systems to `uint` values with a delegate.

   Reason: architecture §3 (Narrative references Core and Data only).
10. **Interaction routing** (App bridge, item 64):
    - `Salvage` and hulks are ignored by beats.
    - `LaneGate` reaches beats only when SPEC-32 refused the jump (`TravelService.Phase == Idle` after its handler). A locked gate tells its story; an open gate just jumps.
    - `Dock` and `Inspect` call `OnBodyInteracted`.
    - `Hail` calls `OnHail` with the encounter.
11. **Folders and namespaces:**
    - `Narrative/Quests/` → `Game.Narrative.Quests`;
    - `Narrative/Beats/` → `Game.Narrative.Beats`;
    - `Narrative/Clues/` → `Game.Narrative.Clues`;
    - `Narrative/ShipLog/` → `Game.Narrative.ShipLog`;
    - definitions in `Game.Data.Definitions` (folder `Definitions/Narrative/`).

## 4. Data model

All types are **public contract** unless marked *internal*. Property rule as in SPEC-30 §4.3. Every definition below implements `ILogicSourceOwner` for its logic fields, with the usages given in the comments (SPEC-40 §4.5).

### 4.1 Prefixes (SPEC-04 registry rows)

| Definition | Prefixes |
|---|---|
| `StoryBeatDefinition` | `beat_` |
| `QuestDefinition` | `quest_`, `contract_` |
| `ClueDefinition` | `clue_` |
| `RumorDefinition : ClueDefinition` | `rumor_` |
| `MysteryDefinition` | `mystery_` |

### 4.2 `StoryBeatDefinition`

```csharp
namespace Game.Data.Definitions
{
    public enum BeatTrigger : byte { Interact = 0, StarSystemEntered = 1, GameStart = 2, Manual = 3, Condition = 4, Hail = 5 }  // add at end only
    public enum BeatRepeat : byte { Once = 0, OncePerBody = 1, Always = 2 }

    [System.Serializable] public sealed class BeatLocation
    {
        public AuthoredStarSystemDefinition starSystem;        // null = any star system
        public string[] bodyTags = System.Array.Empty<string>(); // Interact: any of these storyTags; empty = any interactable body
        public EncounterDefinition hailEncounter;              // Hail: the encounter whose ships were hailed (required for Hail)
    }

    [ContentIdPrefix("beat_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Story Beat", fileName = "beat_new")]
    public sealed class StoryBeatDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private BeatTrigger trigger;
        [SerializeField] private BeatLocation where = new();
        [SerializeField] private Game.Data.Logic.CompiledCondition condition = new();   // BeatCondition; empty = always
        [SerializeField] private int salience;                  // higher wins
        [SerializeField, UnityEngine.Min(0.0001f)] private float weight = 1f;   // tie-break among equal salience
        [SerializeField] private BeatRepeat repeat = BeatRepeat.Once;
        [SerializeField] private bool firstVisitOnly;           // StarSystemEntered: only on the first visit
        [SerializeField] private string yarnNode;               // "" = effect-only beat
        [SerializeField] private Game.Data.Logic.CompiledEffectList onStart = new();    // BeatEffect; applied before the dialog starts
        [SerializeField] private Game.Data.Logic.CompiledEffectList onComplete = new(); // BeatEffect
        [SerializeField] private bool debug;                    // beat_debug_*: excluded from release validation and never auto-selected in release builds
    }
}
```

### 4.3 `QuestDefinition`

```csharp
namespace Game.Data.Definitions
{
    public enum QuestKind : byte { Story = 0, Side = 1, Contract = 2, Debug = 3 }

    [System.Serializable] public sealed class QuestMarker
    {
        public AuthoredStarSystemDefinition starSystem;        // null = no marker
        public string bodyTag;                                 // "" = the star system itself
        public string label;                                   // e.g. "Return to ARK"
    }

    [System.Serializable] public sealed class QuestObjective
    {
        public string id;                                      // snake_case, unique in the quest
        public string text;                                    // shown in the log and the HUD
        public Game.Data.Logic.CompiledCondition doneWhen = new();   // QuestObjective; required
        public Game.Data.Logic.CompiledIntExpr progress = new();     // QuestObjective; empty = no progress display
        public int target;                                     // progress target (0 = none)
        public bool latch = true;                              // stays done once done
        public bool optional;                                  // not required for stage completion
        public QuestMarker marker = new();
    }

    [System.Serializable] public sealed class QuestStage
    {
        public string id;                                      // snake_case, unique in the quest
        public string title;                                   // "" = none
        public System.Collections.Generic.List<QuestObjective> objectives = new();   // 1..16
        public Game.Data.Logic.CompiledEffectList onComplete = new();               // QuestEffect
    }

    [System.Serializable] public struct QuestLogEntry { public string id; [UnityEngine.TextArea(1, 4)] public string text; }

    [ContentIdPrefix("quest_", "contract_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Quest", fileName = "quest_new")]
    public sealed class QuestDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private string title;
        [SerializeField] private string shortDescription;
        [SerializeField, UnityEngine.TextArea(2, 8)] private string description;
        [SerializeField] private QuestKind kind = QuestKind.Side;
        [SerializeField] private bool autoTrack = true;
        [SerializeField] private System.Collections.Generic.List<QuestStage> stages = new();          // 1..16
        [SerializeField] private Game.Data.Logic.CompiledCondition failWhen = new();                  // QuestObjective; empty = never
        [SerializeField] private Game.Data.Logic.CompiledEffectList onAccepted = new();              // QuestEffect
        [SerializeField] private Game.Data.Logic.CompiledEffectList onCompleted = new();             // QuestEffect
        [SerializeField] private Game.Data.Logic.CompiledEffectList onFailed = new();                // QuestEffect
        [SerializeField] private System.Collections.Generic.List<QuestLogEntry> logEntries = new();
        public bool Repeatable => kind == QuestKind.Contract;
    }
}
```

### 4.4 Clues, rumors and mysteries

```csharp
namespace Game.Data.Definitions
{
    [ContentIdPrefix("clue_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Clue", fileName = "clue_new")]
    public class ClueDefinition : ContentDefinition
    {
        [SerializeField] private string title;
        [SerializeField, UnityEngine.TextArea(2, 8)] private string body;
        [SerializeField] private MysteryDefinition mystery;                 // the thread it belongs to (may be null)
        [SerializeField] private System.Collections.Generic.List<ClueDefinition> links = new();   // ship-log arrows
    }

    [ContentIdPrefix("rumor_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Rumor", fileName = "rumor_new")]
    public sealed class RumorDefinition : ClueDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private AuthoredStarSystemDefinition target;       // null = no map marker
        [SerializeField] private string targetBodyTag;                       // "" = the star system
        [SerializeField] private Game.Data.Logic.CompiledCondition resolvedWhen = new();   // MarkerCondition; empty = never resolved
    }

    [ContentIdPrefix("mystery_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Mystery", fileName = "mystery_new")]
    public sealed class MysteryDefinition : ContentDefinition, Game.Data.Logic.ILogicSourceOwner
    {
        [SerializeField] private string title;                               // the question, e.g. "What happened to the Arks?"
        [SerializeField, UnityEngine.TextArea(2, 6)] private string solvedText;
        [SerializeField] private System.Collections.Generic.List<ClueDefinition> requires = new();  // ≥ 1
        [SerializeField] private Game.Data.Logic.CompiledEffectList onSolved = new();             // QuestEffect
        [SerializeField] private int order;                                  // ship-log ordering
    }
}
```

### 4.5 Runtime services (`Game.Narrative.*`)

```csharp
namespace Game.Narrative.Quests
{
    public readonly struct QuestObjectiveView { public readonly string Id, Text; public readonly bool Done, Optional; public readonly int Progress, Target;
        public QuestObjectiveView(string id, string text, bool done, bool optional, int progress, int target) { Id = id; Text = text; Done = done; Optional = optional; Progress = progress; Target = target; } }

    /// <summary>Quests. INarrativeSystem Order 10. Implements SPEC-42 §5.3.</summary>
    public sealed class QuestService : Game.Narrative.INarrativeSystem
    {
        public QuestService(Game.Data.Services.ContentCatalog catalog, FactStore facts, Game.Data.Logic.ILogicContext context, NarrativeService host);
        public int Order => 10;
        public Game.Data.Logic.QuestStatus GetStatus(int questIndex);            // latest instance; NotStarted if none
        public int GetStage(int questIndex);                                     // -1 unless Active
        public int GetInstanceCount(int questIndex);
        public bool IsObjectiveDone(int questIndex, string objectiveId);         // latest instance, current or earlier stage
        public int GetBaseline(in Game.Data.Logic.LogicScope scope, int factSlot, Game.Data.Logic.BaselineKind kind);
        public bool Start(int questIndex);
        public bool Fail(int questIndex);
        public bool ForceComplete(int questIndex);
        public void Track(int questIndex);                                        // 0 = none
        public int TrackedQuestIndex { get; }
        public void AddLog(int questIndex, string entryId);
        public void GetObjectives(int questIndex, System.Collections.Generic.List<QuestObjectiveView> into);   // current stage
        public event System.Action<int> QuestChanged;                            // quest index (status, stage, objective, log, tracking)
        // INarrativeSystem members (§5.3)
    }
}

namespace Game.Narrative.Clues
{
    /// <summary>Clues, rumors, mysteries. INarrativeSystem Order 20. Implements SPEC-42 §5.4.</summary>
    public sealed class ClueService : Game.Narrative.INarrativeSystem
    {
        public ClueService(Game.Data.Services.ContentCatalog catalog, FactStore facts, Game.Data.Logic.ILogicContext context);
        public int Order => 20;
        public bool Reveal(int clueIndex);                                       // true when newly revealed
        public bool IsKnown(int clueIndex);
        public int OrdinalOf(int clueIndex);                                     // 0 = unknown
        public bool IsResolved(int rumorClueIndex);                              // rumors only
        public bool IsSolved(int mysteryIndex);
        public bool IsSeen(int clueIndex); public void MarkSeen(int clueIndex);  // ship-log "new" badge
        public event System.Action<int> ClueChanged;                             // clue index (revealed, resolved, seen)
        public event System.Action<int> MysterySolved;                           // mystery index
    }
}

namespace Game.Narrative.Beats
{
    /// <summary>Story beats. INarrativeSystem Order 30. Implements SPEC-42 §5.2.</summary>
    public sealed class BeatService : Game.Narrative.INarrativeSystem, System.IDisposable
    {
        public BeatService(Game.Data.Services.ContentCatalog catalog, FactStore facts, Game.Data.Logic.ILogicContext context,
                           Game.Narrative.Dialog.DialogService dialog, System.Func<Game.Core.ContentHash, uint> authoredStarSystemValue,
                           System.Func<Game.Core.ContentHash, string> bodyTagOfTemplate, ulong campaignSeed);
        public int Order => 30;
        // Triggers (called by the App bridge, §5.6)
        public bool OnBodyInteracted(in Game.Data.Logic.LogicBodyHandle body, bool playerCanTalk);   // true = a beat was started or queued
        public bool OnHail(Game.Core.ContentHash encounter, ulong memberPersistentId, bool playerCanTalk);
        public void OnStarSystemEntered(uint starSystemValue, bool firstVisit);
        // Effects (through NarrativeLogicAdapter)
        public void Queue(int beatIndex);                                        // queue_beat
        public void CompleteBeat(int beatIndex);                                 // complete_beat (nested)
        public void AbortCurrent();                                              // abort_beat
        // State
        public int CurrentBeatIndex { get; }                                     // 0 = none
        public int QueuedCount { get; }
        public void DebugPlay(int beatIndex);                                    // Narrative Debugger (SPEC-40 §5.9), ignores conditions and repeat
        public void Dispose();                                                   // unsubscribes from DialogService.Ended
    }
}

namespace Game.Narrative.ShipLog
{
    public enum NarrativeMarkerKind : byte { Objective = 0, Rumor = 1 }

    public readonly struct NarrativeMarker
    {
        public readonly NarrativeMarkerKind Kind; public readonly Game.Core.ContentId StarSystem; public readonly string BodyTag; public readonly string Label;
        public readonly int QuestIndex; public readonly int ClueIndex;
        public NarrativeMarker(NarrativeMarkerKind k, Game.Core.ContentId sys, string tag, string label, int quest, int clue)
        { Kind = k; StarSystem = sys; BodyTag = tag; Label = label; QuestIndex = quest; ClueIndex = clue; }
    }

    public readonly struct QuestEntryView { public readonly int QuestIndex; public readonly string QuestId, Title, StageText, ShortDescription;
        public readonly Game.Data.Logic.QuestStatus Status; public readonly bool Tracked; public readonly int Instance;
        public QuestEntryView(int i, string id, string title, string stage, string shortDesc, Game.Data.Logic.QuestStatus st, bool tracked, int inst)
        { QuestIndex = i; QuestId = id; Title = title; StageText = stage; ShortDescription = shortDesc; Status = st; Tracked = tracked; Instance = inst; } }

    public readonly struct ClueEntryView { public readonly int ClueIndex; public readonly string ClueId, Title, Body, MysteryTitle; public readonly int Ordinal; public readonly bool IsNew, IsRumor, Resolved; public readonly Game.Core.ContentId TargetStarSystem;
        public ClueEntryView(int i, string id, string title, string body, string mystery, int ord, bool isNew, bool rumor, bool resolved, Game.Core.ContentId target)
        { ClueIndex = i; ClueId = id; Title = title; Body = body; MysteryTitle = mystery; Ordinal = ord; IsNew = isNew; IsRumor = rumor; Resolved = resolved; TargetStarSystem = target; } }

    public readonly struct JournalEntryView { public readonly string FactName, Text; public readonly int Value;
        public JournalEntryView(string f, string t, int v) { FactName = f; Text = t; Value = v; } }

    /// <summary>Read model for the ship log, HUD objective and maps (SPEC-61), and the Narrative Debugger. Implements SPEC-42 §5.5.</summary>
    public sealed class ShipLogQuery
    {
        public uint Revision { get; }                                    // +1 when anything listed changed
        public event System.Action Changed;
        public void GetQuests(bool completed, System.Collections.Generic.List<QuestEntryView> into);   // Active (or Completed/Failed when completed == true), ordered by accept order
        public void GetObjectives(int questIndex, System.Collections.Generic.List<Game.Narrative.Quests.QuestObjectiveView> into);
        public void GetLog(int questIndex, System.Collections.Generic.List<string> into);              // texts of the latest instance's log
        public bool TryGetTracked(out QuestEntryView quest, out Game.Narrative.Quests.QuestObjectiveView currentObjective);
        public void SetTracked(int questIndex);
        public void GetClues(bool rumors, System.Collections.Generic.List<ClueEntryView> into);        // known only, by ordinal
        public void GetMysteries(System.Collections.Generic.List<(string title, int known, int required, bool solved, string solvedText)> into);
        public void GetJournal(System.Collections.Generic.List<JournalEntryView> into);                // Journal facts with value ≠ 0, by name
        public System.Collections.Generic.IReadOnlyList<NarrativeMarker> Markers { get; }              // rebuilt in AfterEvaluation when dirty
        public void MarkClueSeen(int clueIndex);
    }

    /// <summary>ILogicNarrative implementation (SPEC-40 §4.3). Routes to the three services and SPEC-41.</summary>
    public sealed class NarrativeLogicAdapter : Game.Data.Logic.ILogicNarrative { /* members per SPEC-40 §4.3, routed per §5.5 */ }

    /// <summary>QUST (SPEC-05 order 40, version 1). §5.7.</summary>
    public sealed class QuestSaveParticipant : Game.Core.Save.ISaveParticipant { }
}
```

*Internal* (`Game.Narrative.Quests`):

```csharp
internal sealed class QuestInstance
{
    public int QuestIndex; public int Instance; public Game.Data.Logic.QuestStatus Status; public int Stage;
    public ulong DoneBits;                                   // bit i = objective i of the current stage done (latched); 16 used
    public int[] AcceptBaselineSlots, AcceptBaselineValues;  // sorted by slot
    public int[] StageBaselineSlots, StageBaselineValues;    // sorted by slot; recaptured on stage entry
    public readonly System.Collections.Generic.List<string> Log = new();   // entry IDs in order
    public int AcceptOrder;                                  // monotonic counter value at accept (UI ordering)
}
```

### 4.6 `QUST` chunk (version 1)

| # | Call | Meaning |
|---|---|---|
| 1 | `WriteVarUInt32(n)` | quest instances |
| 2 | per instance, sorted by (quest ID ordinal, instance): `WriteContentId(questId)`, `WriteVarUInt32(instance)`, `WriteByte(status)`, `WriteVarInt32(stage)`, `WriteUInt64(doneBits)`, `WriteVarUInt32(acceptOrder)`, accept baselines (`WriteVarUInt32(k)`, then `WriteString(factName)`, `WriteInt32(value)` sorted by name), stage baselines (same), log (`WriteVarUInt32(k)`, `WriteString(entryId)` in order) | |
| 3 | `WriteContentId(tracked)` (`ContentId.None` when none) | tracked quest |
| 4 | `WriteVarUInt32(n)`, then `WriteContentId(beatId)`, `WriteUInt32(starSystemValue)`, `WriteUInt64(bodyKey)` sorted by (beat ID, star system, key) | `OncePerBody` records |
| 5 | `WriteVarUInt32(n)`, then `WriteContentId(beatId)`, `WriteBool(hasBody)`, `WriteUInt32(starSystem)`, `WriteByte(role)`, `WriteUInt16(slot)`, `WriteUInt16(subSlot)`, `WriteUInt64(templateHash)` in queue order | beat queue |
| 6 | `WriteVarUInt32(n)`, then `WriteContentId(clueId)` sorted | seen clues |
| 7 | `WriteVarUInt32(n)`, then `WriteContentId(rumorId)` sorted | resolved rumors (latched) |

Clue ordinals, mysteries solved and beat counts are generated facts. They are saved by SPEC-40's `FACT` chunk, not here.

### 4.7 Validator rules (`QuestValidatorRules`, `MysteryValidatorRules`; `INarrativeRuleProvider`, Order 20 and 30)

| Code | Severity | Rule |
|---|---|---|
| QST-V001 | Error | A beat's `yarnNode` is not a node of the dialog program (SPEC-41), or a beat has neither a node nor `onStart`/`onComplete` effects |
| QST-V002 | Error | Interact beats: `where.bodyTags` non-empty or `starSystem` set; Hail beats: `hailEncounter` set; GameStart, Manual and Condition beats: `where` empty |
| QST-V003 | Error | A `bodyTags` entry or `QuestMarker.bodyTag` or `targetBodyTag` that is not in the tag vocabulary (§4.10: union of all `BodyTemplateDefinition.storyTag` values and `NarrativeConfigDefinition.knownBodyTags`) |
| QST-V004 | Error | Quest: 1..16 stages; 1..16 objectives per stage; unique stage and objective IDs (snake_case); every `doneWhen` non-empty |
| QST-V005 | Warning | A non-Debug quest never named by any `start_quest` (Yarn command table, effect lists, mystery `onSolved`) |
| QST-V006 | Error | `quest_log <quest> <entry>` names an entry not in that quest's `logEntries` |
| QST-V007 | Error | A clue or rumor never revealed by any `reveal_clue` or `onSolved` (extends NAR-V004) |
| QST-V008 | Error | A mystery with no required clue, or requiring a clue of another mystery |
| QST-V009 | Error | `Contract` quests must be the only repeatable kind; `contract_` IDs must have kind `Contract` and vice versa |
| QST-V010 | Warning | A dialog node not reachable from any beat's `yarnNode` (completes DLG-W022) |
| QST-V011 | Error | `QuestMarker` or rumor with a body tag but no star system |
| QST-V012 | Warning | Two non-debug beats with the same trigger, location and salience and no condition (one always shadows the other unless weights are intended) |

### 4.8 `facts_opening` (new declared facts, opening chapter)

| Name | Kind | Flags | Purpose |
|---|---|---|---|
| `opening_cache_found` | Bool | — | the Drift survey cache was opened (§4.12) |
| `opening_chapter_done` | Bool | Journal (text "The way to Whisper is open.") | set when the gate activates |

Legacy facts come from SPEC-40's flag table (`facts_legacy`).

### 4.9 Ship log journal

`ShipLogQuery.GetJournal` lists every slot with the `Journal` flag whose value is non-zero, ordered by name. Each row shows `JournalTextAt(slot)`, followed by " ×{value}" for Int facts with a value above 1. SPEC-61 shows it under the Clues tab, below the clue list (item 87).

### 4.10 Tag vocabulary and legacy beat placement

**Body tags.** SPEC-30's `storyTag` for each body template; SPEC-30 GAL-012 and SPEC-72 MIG-014 set these values:

| Template | Tag | Template | Tag |
|---|---|---|---|
| `body_station_ark` | `ark_station` | `body_planet_barren` | `barren_planet` |
| `body_station` | `station` | `body_planet_gas_giant` | `gas_giant` |
| `body_station_abandoned` | `abandoned_station` | `body_planet_ice` | `ice_planet` |
| `body_derelict_ship` | `derelict` | `body_planet_ice_giant` | `ice_giant` |
| `body_debris_field` | `debris_field` | `body_planet_rocky` | `rocky_planet` |
| `body_resource_cache` | `resource_cache` | `body_planet_terrestrial` | `terrestrial_planet` |
| `body_gate_dormant` | `gate_dormant` | `body_planet_water` | `ocean_planet` |
| `body_gate_active` | `gate_active` | `body_moon` | `moon` |
| `body_jump_point` | `jump_point` | `body_star` | `star` |
| `body_asteroid_belt` | `asteroid_belt` | `body_star_blackhole` | `black_hole` |

**Placement of converted legacy encounters.** SPEC-40's converter (NAR-015/016) writes these fields:
- the `debug` flag is set exactly for the `beat_debug_*` IDs;
- "Once" beats have `repeat = Once`, all others as listed;
- **Cond.** is the beat condition the converter emits, in addition to any condition it derives from legacy data.

| Legacy | Beat | Trigger | Where | Salience | Weight | Repeat | Cond. / notes |
|---|---|---|---|---|---|---|---|
| `story_waking_up` | `beat_waking_up` | GameStart | — | 100 | 1 | Once | |
| `story_choose_start_ship` | `beat_choose_start_ship` | Manual | — | 0 | 1 | Always | queued by `beat_waking_up` |
| `story_survey_system` | `beat_survey_system` | Manual | — | 0 | 1 | Once | legacy-unreachable; kept for preservation |
| `story_activate_gate` | `beat_gate_dormant` | Interact | `gate_dormant` | 50 | 1 | Always | `not $gate_ark_start_active` |
| `story_use_active_gate` | `beat_gate_active` | Manual | — | 0 | 1 | Always | superseded by SPEC-32 gate jumps |
| `demo_station_hub` | `beat_station_hub` | Interact | `ark_station` | 10 | 1 | Always | |
| `bar_shady_man` | `beat_shady_man` | Manual | — | 0 | 1 | Always | nested from the hub |
| `event_merchant_shady` | `beat_merchant_shady` | Hail | `encounter_traveling_merchant` | 10 | 1 | Always | |
| `event_derelict_salvage` | `beat_derelict_salvage` | Interact | `derelict`, `debris_field`, `resource_cache` | 10 | 1 | OncePerBody | |
| `event_mutiny_end` | `beat_mutiny_end` | Interact | `derelict`, `debris_field` | 20 | 1 | Once | the first derelict tells this story |
| `event_distress_trap` | `beat_distress_trap` | Interact | `derelict`, `debris_field` | 10 | 0.3 | OncePerBody | |
| `event_empty_cargo` | `beat_empty_cargo` | Interact | `derelict`, `resource_cache` | 10 | 0.3 | OncePerBody | |
| `event_crew_stranded` | `beat_crew_stranded` | Interact | `derelict` | 10 | 0.3 | OncePerBody | |
| `event_stable_point` | `beat_stable_point` | Interact | `resource_cache` | 10 | 0.3 | OncePerBody | |
| `event_fuel_station` | `beat_fuel_station` | Interact | `station` | 5 | 1 | Always | |
| `event_singing_storm` | `beat_singing_storm` | Interact | `gas_giant`, `ice_planet`, `rocky_planet`, `ocean_planet` | 10 | 1 | Once | |
| `event_glass_desert` | `beat_glass_desert` | Interact | `barren_planet` | 10 | 1 | Once | |
| `event_frozen_sentinel` | `beat_frozen_sentinel` | Interact | `ice_planet` | 15 | 1 | Once | legacy-unreachable; placed |
| `event_silent_city` | `beat_silent_city` | Interact | `terrestrial_planet` | 10 | 0.5 | Once | |
| `event_overgrown_forge` | `beat_overgrown_forge` | Interact | `terrestrial_planet` | 10 | 0.5 | Once | |
| `event_cryo_vault` | `beat_cryo_vault` | Interact | `ice_giant` | 10 | 1 | Once | |
| `event_time_echo` | `beat_time_echo` | Interact | `jump_point` | 5 | 1 | Once | legacy-unreachable; placed |
| `event_quarantine_lab` | `beat_quarantine_lab` | Interact | `abandoned_station` | 10 | 0.5 | Once | |
| `event_miner_lament` | `beat_miner_lament` | Interact | `abandoned_station` | 10 | 0.5 | Once | |
| `event_scavenger_king` | `beat_scavenger_king` | Interact | `asteroid_belt` | 10 | 1 | Once | legacy-unreachable; placed |
| `event_solar_parasite` | `beat_solar_parasite` | Interact | `star` | 10 | 1 | Once | |
| `event_sensor_ghost` | `beat_sensor_ghost` | StarSystemEntered | — | 5 | 1 | Once | `sys_star_systems_entered >= 4`; legacy-unreachable; placed |
| `event_hollow_moon` | `beat_hollow_moon` | Interact | `moon` | 10 | 1 | Once | |
| `event_horizon_message` | `beat_horizon_message` | Interact | `black_hole` | 10 | 1 | Once | |
| `event_mystery_signal` | `beat_mystery_signal` | StarSystemEntered | `starsys_whisper` | 50 | 1 | Once | `firstVisitOnly` |
| `event_territory_friendly_entry` | `beat_territory_friendly_entry` | StarSystemEntered | — | 20 | 1 | Always | per SPEC-40 quest map; `firstVisitOnly` |
| `event_territory_hostile_entry` | `beat_territory_hostile_entry` | StarSystemEntered | — | 20 | 1 | Always | per SPEC-40 quest map; `firstVisitOnly` |
| `demo_conditional_options` | `beat_debug_conditional_options` | Manual | — | 0 | 1 | Always | debug |
| `demo_audio_events` | `beat_debug_audio_events` | Manual | — | 0 | 1 | Always | debug |

Quest entity options become beats `beat_<quest short>_<option id>`, as in SPEC-40's quest map: trigger Interact, at the tag of the option's entity (`ark_station` for every legacy option), salience 30, repeat Always, with the converted show-and-enable condition.

### 4.11 Converted clues, rumors and mysteries (texts)

**Mysteries:**

| ID | Title | Requires | `solvedText` |
|---|---|---|---|
| `mystery_arks` | What happened to the Arks? | `clue_ark_mission_12`, `clue_ark_mutiny` | "The Arks did not fail. They were turned back — from the inside." |
| `mystery_vanguard` | Who were the Vanguard? | `clue_vanguard_spire`, `clue_vanguard_forge` | "The Vanguard built gates and burned worlds to keep something out." |
| `mystery_loop` | Why do I keep finding my own voice? | `clue_seen_the_loop`, `clue_message_from_self`, `clue_fragmented_warning` | "You have made this journey before. You left yourself directions." |

**Clues:**

| ID | Title | Body | Mystery |
|---|---|---|---|
| `clue_ark_mission_12` | Ark Mission Log 12 | "An encrypted Ark beacon still sings inside the storm: 'Mission 12 — do not follow the lanes past the Silence.'" | `mystery_arks` |
| `clue_ark_mutiny` | The Mutiny | "The derelict's logs end in panic. Half the crew wanted to turn back; the other half sealed the bulkheads." | `mystery_arks` |
| `clue_vanguard_spire` | Glass Spire | "A spire of fused glass marks where a city stood. The Vanguard's beam did this, a thousand years ago." | `mystery_vanguard` |
| `clue_vanguard_forge` | Overgrown Forge | "Vanguard plating, still warm under the vines. Someone kept these forges running long after the war." | `mystery_vanguard` |
| `clue_seen_the_loop` | Seen the Loop | "The sentinel showed you a ship being frozen in place. It was yours." | `mystery_loop` |
| `clue_message_from_self` | A Message From You | "A recording, three cycles old, in your own voice: 'Don't trust the quiet systems.'" | `mystery_loop` |
| `clue_fragmented_warning` | Fragmented Warning | "Static, then a fragment: '…the gate remembers every crossing…'" | `mystery_loop` |

**Rumors** (`resolvedWhen` empty unless stated):

| ID | Title | Body | Target | Body tag | `resolvedWhen` |
|---|---|---|---|---|---|
| `rumor_hidden_cache` | Hidden Cache | "The man at the bar swears there is a supply cache drifting in the asteroid belt nearby." | `starsys_drift` | `resource_cache` | `$opening_cache_found` |
| `rumor_silence_incursions` | Silence Incursions | "Vanguard charts mark systems that went quiet, one after another, along the outer lanes." | — | — | |
| `rumor_satellite_array` | Satellite Array | "Someone left an array listening to the empty city. It is still transmitting." | — | — | |
| `rumor_two_sectors_ahead` | Two Sectors Ahead | "The echo pointed two sectors coreward, where the lanes fold back on themselves." | — | — | |
| `rumor_hidden_planet` | Hidden Planet | "The hollow moon's maps show a planet no chart lists." | — | — | |
| `rumor_hidden_research_station` | Hidden Research Station | "The signal traces back to a research station that does not answer." | `starsys_whisper` | — | `quest_completed("quest_signal_trace")` |

### 4.12 Opening chapter (M3)

These IDs supersede SPEC-30 GAL-012's placeholders (item 86).

**Star systems (`AuthoredStarSystemDefinition`):**

| ID | Name | Position (ly) | Danger | Owner (SPEC-33) | Bodies (key, template, notes) |
|---|---|---|---|---|---|
| `starsys_ark_start` | Ark Reach | (0, 0) | 0 | `faction_ark` | star `body_star`; `ark_station` (`body_station_ark`, Dock, start body, merchant `merchant_station_market`); `reach_i` (`body_planet_rocky`); `reach_ii` (`body_planet_terrestrial`) with moon `reach_ii_a` (`body_moon`); belt `reach_belt` (`body_asteroid_belt`) |
| `starsys_drift` | The Drift | (6, 2) | 2 | none | star `body_star`; belt `drift_belt` (`body_asteroid_belt`); `drift_derelict` (`body_derelict_ship`); `drift_cache` (`body_resource_cache`, inside the belt's outer edge); `drift_lab` (`body_station_abandoned`) |
| `starsys_whisper` | Whisper | (−5, 4) | 1 | `faction_vestari` | star `body_star`; `whisper_i` (`body_planet_ice_giant`); `whisper_ii` (`body_planet_barren`); `whisper_outpost` (`body_station`, Dock, a blueprint with turret blocks so it defends itself) |

**Lanes (`AuthoredLane`):**
- `ark_start ↔ drift`: open, default gates (`body_jump_point`).
- `ark_start ↔ whisper`: gated by `$gate_ark_start_active`, with locked message "The gate is dormant."; gate template `body_gate_dormant` on both ends.

**New writing**, in `Assets/Game/Content/Narrative/Dialog/Opening/opening.yarn`:
- `beat_drift_survey_cache`: Interact at `resource_cache` in `starsys_drift`; salience 40; Once; condition `quest_active("quest_survey_local_materials")`.
  - Node `opening_cache`: two lines ("A battered cargo pod, tagged with an ARK survey stencil." / "Inside: refined palladium, enough for NORA's repairs.") and one option "Take it", which does:
    ```
    <<give_item resource_palladium 3>>
    <<set_fact opening_cache_found true>>
    <<stop>>
    ```
- `beat_gate_awakens`: Condition trigger, condition `$gate_ark_start_active and not $opening_chapter_done`; Once; effect-only.
  - `onComplete`: `set_fact opening_chapter_done true`, `notify "The gate to Whisper hums awake."`.

**Intended path**, verified by QST-010:
1. new game → `beat_waking_up` → `beat_choose_start_ship` → probes spawn at `ark_station` and are fought;
2. dock at ARK → hub: accept `quest_survey_local_materials`; the bar gives `rumor_hidden_cache`;
3. jump to the Drift → open the cache (palladium); the derelict gives `clue_ark_mutiny` (the first clue);
4. return → hand over (`beat_survey_local_materials_turn_in_raw_materials_at_ark`) → `story_survey_complete` → `beat_gate_dormant` activates the gate (`gate_ark_start_active`) → `beat_gate_awakens`;
5. jump to Whisper → `beat_mystery_signal`.

## 5. Systems, services and flow

### 5.1 Player-free gating (App bridge, `NarrativeTickBridgeSystem`)

`SetPlayerFree(free)` receives:

```
free = UINavigator has no Screens/Modals entry && !dialog.IsActive
       && TravelService.Phase == Idle                       (item 65)
       && player ship exists and is not a hulk
       && PlayerCombatStatus.HostilesEngaging == 0          (read after CompleteDependencyBeforeRO)
```

`playerCanTalk` passed to Interact/Hail triggers is `UINavigator has no Modals entry && !dialog.IsActive && TravelService.Phase == Idle`. The player pressed the key, so open screens such as the dock screen and combat do not block.

### 5.2 `BeatService`

**Construction:**
- pre-index beats by trigger;
- for Interact beats, build a tag → beat-list map;
- for Hail beats, an encounter hash → list map;
- for StarSystemEntered beats, a list;
- for Condition beats, a `DependencyIndex` over their conditions.

Release builds skip `debug` beats when indexing.

**Selection** `Select(candidates, contextKey, starSystemValue, firstVisit) → beatIndex or 0`:
1. For each candidate (beat ID order):
   1. Skip it if any of these holds:
      - `where.starSystem != null && authoredStarSystemValue(where.starSystem.Id.Hash) != starSystemValue`;
      - repeat `Once` and its fact > 0;
      - repeat `OncePerBody` and `(beat, starSystem, contextKey)` is recorded;
      - `firstVisitOnly && !firstVisit`.
   2. Push `LogicScope(BeatCondition, 0, 0, beatIndex, hasBody, body)`; evaluate `condition`; pop. Skip it if false.
   3. Keep it if its salience is the maximum seen. Reset the kept list when a higher salience appears.
2. If the kept list is empty, return 0. One beat → return it.
3. Several beats: `rng = RngStreams.For(campaignSeed, RngPurpose.Narrative, StableHash.Combine((ulong)facts.GetBySlot(sysBeatsCompletedSlot), contextKey))`. Take `u = rng.NextFloat()` and pick the first beat whose cumulative weight is `> u × Σweight`, in beat ID order.

**Triggers:**

| Method | Behaviour |
|---|---|
| `OnBodyInteracted(body, canTalk)` | `tag = bodyTagOfTemplate(body.Template)`. Candidates = Interact beats with that tag in `bodyTags`, plus Interact beats with an empty `bodyTags` whose `where.starSystem` is set. `contextKey = body.Key`. If `!canTalk` → return false. Otherwise `Select`; 0 → false; else `Play(beat, scope with body)` → true |
| `OnHail(encounter, member, canTalk)` | Candidates from the hail map. `contextKey = member`. Same rules, scope without a body |
| `OnStarSystemEntered(sys, firstVisit)` | Candidates = StarSystemEntered beats. If `Select` returns a beat: `Enqueue(beat, scope none)` (played when free). Only the single best beat is queued per entry |
| Condition (in `Evaluate`) | For each dirty Condition beat (dependency index, or all when `fullReevaluate`): if its repeat rule allows, it is not already queued and `condition` is true → `Enqueue`. After a load, `fullReevaluate` enqueues only beats whose condition is true **and** whose repeat rule still allows. `Once` beats completed before the save are therefore never replayed (decision 6) |
| GameStart | `OnNewGame` sets `gameStartPending`. The first `AfterEvaluation(free == true)` with the world ready enqueues every GameStart beat, ordered by (salience desc, ID). `gameStartPending` is not saved (a new game cannot be saved before its first frame) |

**Queue.** A FIFO of `(beatIndex, LogicScope)`, holding at most 32 entries. On overflow, drop the oldest Condition-triggered entry and log `GameLog.Warn`.

`AfterEvaluation(free)`: if `free`, no beat is current, `!dialog.IsActive` and the queue is non-empty, pop the head and `Play` it. At most one beat starts per frame.

**`Play(beat, scope)`:**
1. Set `current = (beat, scope)` and `abortRequested = false`.
2. Push `scope` with usage `BeatEffect` and apply `onStart`. If the effects are rejected (unaffordable), trace `EffectRejected` and continue.
3. Empty `yarnNode` → `CompleteCurrent(Completed)` immediately.
4. Otherwise `dialog.TryStart(new DialogRequest(beatIndex, yarnNode, scope))`. If that returns false, re-insert the beat at the queue head, clear `current` and return. It is retried next frame.

**`DialogService.Ended` handler.** It is subscribed after SPEC-41's presenter, so the dialog screen has already popped:
1. Ignore the event unless `e.BeatIndex == current.Beat`.
2. **Outcome:**
   - `e.Outcome == Completed && !abortRequested` → `CompleteInternal(current.Beat, current.Scope)`;
   - `Aborted`, `Interrupted`, or `abortRequested` → no count.
3. Clear `current`.
4. If `e.PendingTerminal != null`: push `e.Scope` (usage `BeatEffect`), call `e.PendingTerminal.Apply(context)`, pop. Terminal commands: `start_combat`, `open_ship_editor`, `request_lane_jump`.

**`CompleteInternal(beat, scope)`:**
1. `facts.AddBySlot(beatSlot, 1, Beat)`; `facts.AddBySlot(sysBeatsCompletedSlot, 1, Beat)`.
2. For `OncePerBody`, record `(beat, scope.Body.StarSystem, scope.Body.Key)`.
3. Push `scope` (usage `BeatEffect`) and `onComplete.ApplyAll`; pop.
4. Trace `BeatCompleted`.

**Effects:**
- `CompleteBeat(beatIndex)` (nested): `CompleteInternal(beatIndex, current.Scope)` for a beat that is **not** the current one. If it is the current one, set `completedByEffect` so the `Ended` handler does not count it twice.
- `AbortCurrent()`: `abortRequested = true`.
- `Queue(i)`: `Enqueue(i, current?.Scope ?? none)`.

`DebugPlay(i)`: `Play(i, none)`, ignoring repeat rules and the condition.

### 5.3 `QuestService`

**Index.**
- One `DependencyIndex` per service. The owner is the quest index; dependencies are all of `doneWhen`, `progress` and `failWhen` over every stage.
- Per quest, a sorted `int[] baselineSlots`: the fact slots of every `LogicDependency(Fact, slot)` its conditions report. `since_*` arguments are fact reads, so they are included.

**`Start(q)`** (decision 5):
1. Check eligibility; if not eligible, return false.
2. New `QuestInstance`: `Status = Active`, `Stage = 0`, `DoneBits = 0`, `AcceptOrder = ++acceptCounter`.
3. Capture accept and stage baselines: `value = facts.GetBySlot(slot)` for each slot in `baselineSlots`.
4. Push `LogicScope(QuestEffect, q, instance, 0, false, default)`; `onAccepted.ApplyAll`; pop.
5. If `autoTrack || TrackedQuestIndex == 0`, track it.
6. `host.NotifyDependency(Quest, q)`; mark `q` dirty; raise `QuestChanged(q)`; trace `QuestAccepted`.

**`Evaluate(changed, full)`.** For each dirty active quest (the union over `changed`, or all when `full`), with the scope `LogicScope(QuestObjective, q, instance, 0, false, default)` pushed:
1. If `failWhen` is non-empty and evaluates true → `Fail(q)`; continue.
2. **Loop** at most `stages.Count` times:
   1. For each objective `i` of `Stage`:
      - `done = (latch && bit i set) || doneWhen.Evaluate`;
      - set or clear bit `i` (clear only if `!latch`).
   2. If not every non-optional objective is done, break.
   3. Otherwise the stage is complete:
      - **Restore mode** (the first evaluation after `OnRestored`) → break without effects. The saved stage and bits are authoritative (decision 6).
      - **Normal mode:**
        1. apply `stages[Stage].onComplete` (scope `QuestEffect`);
        2. trace `StageCompleted`;
        3. last stage → `CompleteQuest(q)` and break;
        4. otherwise `Stage++`, `DoneBits = 0`, recapture the stage baselines, `QuestChanged(q)`, and continue the loop (the new stage may already be done; its `since_stage` objectives cannot be).
3. If any bit changed, raise `QuestChanged(q)`.

**`CompleteQuest(q)`:**
1. `Status = Completed`;
2. apply `onCompleted`;
3. if tracked, retrack the most recent other active quest by `AcceptOrder` (or none);
4. `NotifyDependency(Quest, q)`, `QuestChanged`, trace.

**`Fail(q)`** works like `CompleteQuest`, with `Failed` and `onFailed`.

**`ForceComplete(q)`** (`complete_quest`): only when Active. `Status = Completed`, apply `onCompleted` only (stage effects skipped, as NAR-W010 warns).

**`GetBaseline(scope, slot, kind)`:**
1. instance = `scope.QuestIndex` / `scope.QuestInstance`.
2. Binary-search `slot` in the accept or stage arrays.
3. Not found → `GameLog.ErrorOnce` and return the current fact value. The objective then reads 0 progress (fail closed).

**`AddLog(q, entryId)`:** append to the latest instance when the entry exists in the definition (the validator guarantees it at content time; at runtime an unknown entry → `WarnOnce`); `QuestChanged`.

**`Track(q)`:** `q == 0` or an active quest; `QuestChanged` for the old and new quest.

**`OnNewGame`** clears everything. **`OnRestored`** sets restore mode for the next evaluation and marks all quests dirty.

### 5.4 `ClueService`

- **`Reveal(c)`:**
  1. If `facts.GetBySlot(clueSlot(c)) != 0`, return false.
  2. `n = facts.GetBySlot(counterSlot) + 1`; `facts.SetBySlot(counterSlot, n, Clue)`; `facts.SetBySlot(clueSlot, n, Clue)`.
  3. Raise `ClueChanged(c)` and trace `ClueRevealed`.
  4. For each mystery `m` that requires `c` (reverse index): if all of its clues are known and `fact(m) == 0`: `facts.SetBySlot(mysterySlot, 1, Clue)`; apply `onSolved` (scope `QuestEffect`, no quest); raise `MysterySolved(m)`; trace.
  5. Mark the markers dirty.
- **Rumor resolution.** A `DependencyIndex` over `resolvedWhen` conditions of known rumors. In `Evaluate`, a dirty known rumor whose condition is true is added to `resolvedRumors` (latched and saved), with `ClueChanged` and markers dirty. On a restored full evaluation, rumors that are now true are latched silently. No effects are attached to resolution.
- **Seen:** `MarkSeen` adds to `seenClues` and raises `ClueChanged`.

### 5.5 `NarrativeLogicAdapter`, `ShipLogQuery` and markers

**Adapter routes** (SPEC-40 §4.3):

| Member | Routed to |
|---|---|
| `GetQuestStatus`, `GetQuestStage`, `IsObjectiveDone`, `GetBaseline`, `StartQuest`, `FailQuest`, `CompleteQuest` (→ `ForceComplete`), `TrackQuest`, `AddQuestLog` | `QuestService` |
| `RevealClue` | `ClueService.Reveal` |
| `QueueBeat`, `CompleteBeat`, `AbortCurrentBeat` | `BeatService` |
| `GetDialogVisitCount` | `dialog.Memory.GetVisitCount` |
| `Roll` | `dialog.Roll` |

`IsObjectiveDone` checks the latest instance:
- `true` if the objective belongs to an earlier stage, or to the current stage with its bit set;
- `false` otherwise, or when the quest is not Active/Completed;
- a Completed quest → all its objectives are `true`.

**`ShipLogQuery`.** It subscribes to `QuestChanged`, `ClueChanged`, `MysterySolved` and `EventBus<FactChanged>` (Journal flags only, a slot bitset check). It increments `Revision` and raises `Changed` at most once per frame (coalesced in `AfterEvaluation` through an internal hook that BeatService forwards).

Its list methods fill the caller's lists without allocating per call, beyond the strings already stored in definitions.

**Markers**, rebuilt when dirty:
1. For the tracked quest, the current stage's not-done objectives with `marker.starSystem != null`: `NarrativeMarker(Objective, starSystem.Id, bodyTag, label or objective text, q, 0)`.
2. Then known, unresolved rumors with a target: `NarrativeMarker(Rumor, target.Id, targetBodyTag, title, 0, c)`.

SPEC-61 resolves `StarSystem` to a map position and `BodyTag` to a body of the loaded star system (item 87).

### 5.6 App bridge rules (SPEC-40 `NarrativeWorldEventBridge`, item 64)

| Event | Narrative handling added by this spec |
|---|---|
| `BodyInteracted` | `Kind == Salvage` or the target is a hulk → nothing. `Kind == LaneGate` → only if `TravelService.Phase == Idle` (the jump was refused). `Kind == Hail` → `encounters.TryGetInstanceByMember(e.GridPersistentId, out info)` → `beatService.OnHail(info.Definition.Id.Hash, pid, canTalk)`. Otherwise (`Dock`, `Inspect`, `LaneGate` refused): build `LogicBodyHandle` (`BodyId.FromLocalKey(e.StarSystemValue, e.BodyKey)` fields and the template hash from `GalaxyService`), then `beatService.OnBodyInteracted(handle, canTalk)`. `false` and `Kind == Inspect` → `InteractionUnhandled` ("Nothing of interest") |
| `StarSystemEntered` | `beatService.OnStarSystemEntered(e.StarSystemValue, e.VisitCount == 1)` |

`authoredStarSystemValue(hash) = galaxy.TryResolveStarSystem(catalog.Get(hash).Id, out id) ? id.Value : 0`. `bodyTagOfTemplate(hash)` returns the template's `storyTag`, or "" when it has none. Both are dictionaries built once at boot.

### 5.7 `QUST` participant

- **Capture** writes §4.6 from the services. Baselines are written by fact **name** (`FactCatalog.IdAt(slot).Name`).
- **Restore:**
  1. Clear the services.
  2. Read everything:
     - unknown quest IDs → the records are kept raw and written back unchanged;
     - unknown fact names in baselines → kept as names and resolved on use (unresolvable → `ErrorOnce`, value 0);
     - unknown beats and clues → kept raw.
  3. Call nothing else. `NarrativeService.OnSessionRestored()` triggers `OnRestored` on each system, and no effects run.
- The round trip is byte-identical.

## 6. Public API

| Call | Pre-conditions | Post-conditions / errors |
|---|---|---|
| `BeatService.OnBodyInteracted/OnHail/OnStarSystemEntered` | Main thread (bridge, inside `EventHub.DrainAll`) | §5.2 |
| `QuestService.Start/Fail/ForceComplete/Track/AddLog` | Main thread (effects) | §5.3; idempotent where stated |
| `ClueService.Reveal` | Main thread | §5.4 |
| `ShipLogQuery.*` | Main thread | Read model; `SetTracked`/`MarkClueSeen` are the only writes |
| `QuestSaveParticipant` | SPEC-05 rules | §5.7 |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| A quest is accepted after its target fact already reached its value (05-B3) | `since_accept` objectives start at 0; the quest does not complete instantly |
| Load a save where a stage was 1 objective short and the fact is now satisfied | Restore mode keeps the saved bits silently; the stage completes only on the next change of that dependency (no effects during load) |
| A quest definition removed after a save (05-B8) | Its record is preserved raw and never completes or applies effects |
| `start_quest` on a completed story quest (05-B7) | No-op |
| A contract accepted twice | Two instances; each completes on its own; `GetStatus` reports the latest |
| Two Interact beats with equal salience at one body | Seeded weighted pick, stable for the same fact state |
| Interact while another dialog is active | `canTalk == false` → the beat does not start; nothing is queued |
| A StarSystemEntered beat while in combat | Queued; plays when the player is free |
| Dialog start fails (unknown node) | Beat re-queued at the head; SPEC-41 logs an error; the validator prevents this in shipped content |
| `abort_beat` then `<<stop>>` | Outcome `Completed`, but `abortRequested` → not counted |
| Nested `complete_beat beat_shady_man` inside the hub | `beat_shady_man` counts and runs `onComplete`; the hub is unaffected |
| Terminal `request_lane_jump` | Dialog popped; the beat counted (if completed); then SPEC-32 `RequestJump(Narrative)` |
| Rumor resolved before it was revealed | Not shown; when revealed it is latched as resolved on the next evaluation and never shows a marker |
| Queue overflow (more than 32) | Oldest Condition-triggered entry dropped with a warning |
| Lane gate pressed while locked | SPEC-32 refuses (notice) and `beat_gate_dormant` plays |

## 8. Performance budget

| Item | Target | Test (task) |
|---|---|---|
| Idle frame (no changes) for all three services | ≤ 0.02 ms, 0 B (inside SPEC-40's `NarrativeService.Tick` budget) | `NarrativeSystemsPerfTests.Idle` (QST-005) |
| One fact change touching 50 objectives | ≤ 0.1 ms, 0 B | `NarrativeSystemsPerfTests.OneChange50Objectives` (QST-003) |
| Beat selection at a body with 40 candidates | ≤ 0.1 ms | `BeatServiceTests.Select40_Time` (QST-005) |
| `QUST` capture with 60 quest instances | ≤ 0.5 ms | `QuestSaveTests.Capture_Time` (QST-007) |

**Scale notes.**
- Selection is O(candidates for a tag), not O(beats).
- Objective evaluation is driven by the dependency index (O(subscribers)).
- Baselines are per quest, over the slots it reads (tens).
- Markers are rebuilt only when dirty.
- Rejected:
  - polling objectives per frame (05-B10);
  - string facts at runtime;
  - storing texts in saves.

## 9. Tasks

### QST-001 — Public contract types
- **Milestone:** M3
- **Tier:** standard — transcribes the exact code of §4 (bodies throw `NotImplementedException`)
- **Depends on:** `CORE-001`, `DATA-001`, `NAR-001`, `DLG-001`, `GAL-001`, `ENC-001`
- **Do:**
  1. Create every type of §4 in the folders of decision 11.
  2. Register the five prefixes (§4.1) through `[ContentIdPrefix]`.
  3. Add `CatalogRefResolver` lines for `Quest`, `Clue` (including rumors), `Beat` and `Mystery` in SPEC-40's `RefResolverRegistry.CreateDefault`, if not already present.
- **Files:** `Assets/Game/Data/Definitions/Narrative/{BeatTrigger,BeatRepeat,BeatLocation,StoryBeatDefinition,QuestKind,QuestMarker,QuestObjective,QuestStage,QuestLogEntry,QuestDefinition,ClueDefinition,RumorDefinition,MysteryDefinition}.cs`; `Assets/Game/Narrative/Quests/{QuestService,QuestObjectiveView,QuestInstance}.cs`; `Assets/Game/Narrative/Clues/ClueService.cs`; `Assets/Game/Narrative/Beats/BeatService.cs`; `Assets/Game/Narrative/ShipLog/{NarrativeMarkerKind,NarrativeMarker,QuestEntryView,ClueEntryView,JournalEntryView,ShipLogQuery,NarrativeLogicAdapter,QuestSaveParticipant}.cs`
- **Acceptance criteria:**
  - [ ] Compiles; `Game.Narrative` references no Galaxy, Simulation or Economy assembly.
- **Tests:** `QuestContractTests.NarrativeAsmdef_NoGalaxy`.

### QST-002 — Definitions, logic compile/bind and validator rules
- **Milestone:** M3
- **Tier:** standard — fields, usages and rule table given in §4.2–§4.7
- **Depends on:** `QST-001`, `NAR-003`, `NAR-004`, `NAR-006`, `NAR-009`, `NAR-013`
- **Do:**
  1. Implement the properties and `ILogicSourceOwner` on the five definitions, with the usages in the comments of §4.2–§4.4. Field paths are `stages[i].objectives[j].doneWhen` and so on.
  2. Implement `QuestValidatorRules` (V004–V006, V009, V011) and `MysteryValidatorRules` (V007, V008), plus the beat rules (V001–V003, V010, V012), in one provider class per file.
- **Files:** `Assets/Game/Data/Definitions/Narrative/*.cs`; `Assets/Game/Editor/Narrative/{QuestValidatorRules,MysteryValidatorRules,BeatValidatorRules}.cs`; `Assets/Game/Tests/EditMode/Editor/Narrative/QuestValidatorTests.cs`
- **Acceptance criteria:**
  - [ ] Each rule has a failing and a passing fixture.
  - [ ] Every logic field compiles with its usage; for example, `roll()` in a `doneWhen` fails with NAR-E008.
- **Tests:** `QuestValidatorTests.<Code>_Fails`, `.<Code>_Passes`, `.Usage_Enforced`.

### QST-003 — QuestService
- **Milestone:** M3
- **Tier:** frontier — F3: baselines and restore mode are what prevent the 05-B3/B8 classes; the errors are silent
- **Depends on:** `QST-002`, `NAR-005`, `NAR-007`, `NAR-010`, `NAR-011`
- **Do:** Implement `QuestService` per §5.3: index, `Start`, `Evaluate` (normal and restore modes), stages, `Fail`, `ForceComplete`, `Track`, `AddLog`, `GetBaseline`, `GetObjectives`.
- **Files:** `Assets/Game/Narrative/Quests/{QuestService,QuestInstance}.cs`; `Assets/Game/Tests/EditMode/Narrative/{QuestServiceTests,NarrativeSystemsPerfTests}.cs`
- **Acceptance criteria:**
  - [ ] **05-B3:** a fact at 5 before accept, with `since_accept(fact) >= 1` → not done until the fact becomes 6.
  - [ ] Stage progression applies the effects in order, recaptures stage baselines, and continues into an already-satisfied next stage in the same evaluation.
  - [ ] Restore mode applies no effects and keeps the saved bits.
  - [ ] `start_quest` is idempotent per decision 5; contracts get instance 1, 2, …
  - [ ] `failWhen` fails an active quest once.
  - [ ] `OneChange50Objectives` meets §8.
- **Tests:** `QuestServiceTests.SinceAccept_NotRetroactive`, `.Stages_ProgressAndBaselines`, `.Restore_NoEffects`, `.Start_Idempotent`, `.Contract_Instances`, `.FailWhen_Once`, `.Track_RetracksOnComplete`; `NarrativeSystemsPerfTests.OneChange50Objectives`.

### QST-004 — ClueService
- **Milestone:** M3
- **Tier:** standard — algorithm in §5.4
- **Depends on:** `QST-002`, `NAR-005`, `NAR-007`, `NAR-010`, `NAR-011`
- **Do:** Implement `ClueService` per §5.4: reveal ordinals, mystery solve, rumor resolution, seen state.
- **Files:** `Assets/Game/Narrative/Clues/ClueService.cs`; `Assets/Game/Tests/EditMode/Narrative/ClueServiceTests.cs`
- **Acceptance criteria:**
  - [ ] Ordinals are 1, 2, 3 in reveal order; revealing again changes nothing.
  - [ ] A mystery solves once and applies `onSolved` once.
  - [ ] A rumor resolves when its condition becomes true, latches, and survives restore without effects.
- **Tests:** `ClueServiceTests.Ordinals`, `.Reveal_Idempotent`, `.Mystery_SolvesOnce`, `.Rumor_ResolvesAndLatches`, `.Restore_Silent`.

### QST-005 — BeatService
- **Milestone:** M3
- **Tier:** frontier — F5: triggers, queue, dialog lifecycle, nested completion and terminal commands across frames
- **Depends on:** `QST-003`, `QST-004`, `DLG-006`, `DLG-007`, `DLG-008` (ADR-0102)
- **Do:** Implement `BeatService` per §5.2: indexes, selection, triggers, queue, `Play`, the `Ended` handler, `CompleteInternal`, effects and `DebugPlay`.
- **Files:** `Assets/Game/Narrative/Beats/{BeatService,BeatQueue}.cs`; `Assets/Game/Tests/EditMode/Narrative/BeatServiceTests.cs` (fake `IDialogRunner` through SPEC-41's test seam)
- **Acceptance criteria:**
  - [ ] The highest salience wins; ties are deterministic for the same fact state and differ when `sys_beats_completed` differs.
  - [ ] `Once` beats never replay, including after save/load; `OncePerBody` replays at another body.
  - [ ] GameStart beats play on the first free frame, in order.
  - [ ] A Condition beat queues when its condition flips true and plays when free.
  - [ ] A completed dialog counts once; `abort_beat` and player abort do not count.
  - [ ] A nested `complete_beat` counts the nested beat only.
  - [ ] A terminal effect runs after the dialog popped, with the beat scope.
  - [ ] `TryStart` failure re-queues the beat.
  - [ ] Idle and `Select40` meet §8.
- **Tests:** `BeatServiceTests.Salience_And_TieBreak`, `.Once_NeverReplays`, `.OncePerBody`, `.GameStart_Order`, `.ConditionBeat_QueuesAndPlays`, `.Count_OnlyOnCompleted`, `.NestedComplete`, `.Terminal_AfterPop`, `.TryStartFails_Requeues`, `.Select40_Time`; `NarrativeSystemsPerfTests.Idle`.

### QST-006 — Adapter, ship log and markers
- **Milestone:** M3
- **Tier:** standard — routing tables and read model in §5.5
- **Depends on:** `QST-005`
- **Do:** Implement `NarrativeLogicAdapter`, `ShipLogQuery` (including journal and coalesced `Changed`) and markers (§5.5).
- **Files:** `Assets/Game/Narrative/ShipLog/{NarrativeLogicAdapter,ShipLogQuery,NarrativeMarkers}.cs`; `Assets/Game/Tests/EditMode/Narrative/ShipLogQueryTests.cs`
- **Acceptance criteria:**
  - [ ] Every `ILogicNarrative` member routes as tabled (fakes).
  - [ ] The ship log lists only known clues by ordinal.
  - [ ] Journal rows appear for non-zero Journal facts.
  - [ ] Markers show the tracked objective and unresolved rumors, and update on change only (`Revision`).
  - [ ] List methods allocate 0 B after warm-up.
- **Tests:** `ShipLogQueryTests.Adapter_Routes`, `.Clues_KnownByOrdinal`, `.Journal_NonZero`, `.Markers_TrackedAndRumors`, `.Revision_OnChangeOnly`, `.Lists_NoAlloc`.

### QST-007 — QUST participant
- **Milestone:** M3
- **Tier:** frontier — F3: binary save format with raw preservation and name-keyed baselines
- **Depends on:** `QST-006`, `PERS-001`
- **Do:** Implement `QuestSaveParticipant` (§4.6, §5.7) and the SPEC-05 registry row `QUST`.
- **Files:** `Assets/Game/Narrative/ShipLog/QuestSaveParticipant.cs`; `Assets/Game/Tests/EditMode/Narrative/QuestSaveTests.cs`
- **Acceptance criteria:**
  - [ ] The round trip is byte-identical. The test state contains:
    - 60 instances (including contracts);
    - baselines;
    - logs;
    - the tracked quest;
    - per-body records;
    - a queued beat;
    - seen clues;
    - resolved rumors;
    - one unknown quest, beat and clue.
  - [ ] Restore applies no effects.
  - [ ] Capture time meets §8.
- **Tests:** `QuestSaveTests.RoundTrip_ByteIdentical`, `.Unknown_Preserved`, `.Restore_NoEffects`, `.Capture_Time`.

### QST-008 — Bridge rules, player-free gating and registration
- **Milestone:** M3
- **Tier:** standard — routing rules of §5.1 and §5.6
- **Depends on:** `QST-005`, `QST-006`, `SPEC-40`, `SPEC-32`, `SPEC-33`
- **Do:**
  1. Extend `NarrativeWorldEventBridge` with the §5.6 rules.
  2. Extend `NarrativeTickBridgeSystem` with the §5.1 `free` and `canTalk` values.
  3. In `NarrativeBootstrap.Build`: construct and register `QuestService`, `ClueService` and `BeatService` (with the two delegates of §5.6 and `campaignSeed`); construct `ShipLogQuery` and `NarrativeLogicAdapter`, and set it into `LogicContext`; register `QuestSaveParticipant`.
- **Files:** `Assets/Game/App/Services/{NarrativeWorldEventBridge,NarrativeBootstrap}.cs`; `Assets/Game/Presentation/Systems/NarrativeTickBridgeSystem.cs`; `Assets/Game/Tests/EditMode/App/NarrativeBridgeBeatTests.cs`
- **Acceptance criteria:**
  - [ ] A locked lane gate press plays `beat_gate_dormant`; an open gate press starts a jump and no beat.
  - [ ] A Hail on a travelling merchant plays `beat_merchant_shady`.
  - [ ] A Salvage press never reaches beats.
  - [ ] `free` is false during travel and while hostiles engage.
- **Tests:** `NarrativeBridgeBeatTests.LockedGate_PlaysBeat`, `.OpenGate_NoBeat`, `.Hail_PlaysMerchant`, `.Salvage_Ignored`, `.Free_FalseInTravelAndCombat`.

### QST-009 — Opening chapter content
- **Milestone:** M3
- **Tier:** standard — content from the tables of §4.8, §4.10–§4.12
- **Depends on:** `QST-002`, `GAL-003`, `NAR-013`, `DLG-003`, `ENC-002`, `ECO-002`
- **Do:**
  1. Create `facts_opening.asset`, `opening.yarn` with the two new beats, the three mysteries, the seven clues and the six rumors (§4.11).
  2. Set `storyTag` on every listed body template (§4.10).
  3. Author or adjust the three star systems and two lanes of §4.12 in SPEC-30's content, replacing the GAL-012 placeholders, together with the dependent content of other specs (item 86).
  4. Set `NarrativeConfigDefinition.knownBodyTags` to the §4.10 tags.
- **Files:** `Assets/Game/Content/Narrative/{Facts/facts_opening.asset,Dialog/Opening/opening.yarn,Beats/beat_drift_survey_cache.asset,Beats/beat_gate_awakens.asset,Clues/*.asset}`; `Assets/Game/Content/Bodies/*.asset`; `Assets/Game/Content/Galaxy/*.asset`
- **Acceptance criteria:**
  - [ ] Zero NAR, DLG, QST, GAL, ENC and ECO errors over the whole content database.
  - [ ] Every tag used by a converted beat has at least one template.
- **Tests:** `OpeningContentTests.ValidatesClean`, `.TagsCovered`.

### QST-010 — Opening chapter playthrough
- **Milestone:** M3
- **Tier:** standard — scripted scenario through the dev harness with given checkpoints
- **Depends on:** `QST-008`, `QST-009`, `QST-007`, `SPEC-70`, `SPEC-32`, `SPEC-50`
- **Do:** Write `TestData/Scenarios/opening_chapter.scenario` and a PlayMode wrapper test that run the §4.12 path with real inputs:
  1. dialog choices by number keys;
  2. `interact body=…`;
  3. `jump to=…`;
  4. `encounter combat=…` only to speed up the probe fight.

  Checkpoints, using SPEC-70 probes `fact()`, `inv()`, `star_system` and `travel.phase`, plus the quest probes added here (`quest("id")` returns the status name, `clue("id")` the ordinal):
  - after the intro: `fact("intro_awake") == 1`;
  - after the hub: `quest("quest_survey_local_materials") == "Active"`;
  - after the Drift: `inv("resource_palladium") >= 3 && clue("clue_ark_mutiny") == 1`;
  - after the hand-over: `fact("gate_ark_start_active") == 1`;
  - final: `star_system == <whisper>`, `fact("beat_mystery_signal") == 1`.

  Add a save/load at the Drift checkpoint and assert that nothing replays.
- **Files:** `TestData/Scenarios/opening_chapter.scenario`; `Assets/Game/App/DevRuntime/Scenarios/QuestScenarioProbes.cs`; `Assets/Game/Tests/PlayMode/Narrative/OpeningChapterTests.cs`
- **Acceptance criteria:**
  - [ ] The scenario passes from a new game with zero errors and exceptions logged.
  - [ ] Each checkpoint holds.
  - [ ] After the mid-run load, no `Once` beat plays twice.
- **Tests:** `OpeningChapterTests.Playthrough`, `.SaveLoad_NoReplays`; scenario `opening_chapter`.

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Opening chapter star systems and their names | Ark Reach, The Drift, Whisper (§4.12) | Owner / narrative lead |
| 2 | Placement of legacy-unreachable encounters (frozen sentinel, time echo, scavenger king, sensor ghost) | As in §4.10 | Narrative lead |
| 3 | Clue and rumor texts for converted flags | §4.11 drafts | Narrative lead |
| 4 | Procedural contracts (parameterised templates) | Not in v1; three fixed repeatable contracts | Owner |
| 5 | Should combat block automatic story beats? | Yes (§5.1) | Owner |
| 6 | Requested changes to other specs | `_INTEGRATION-TODO.md` items 64, 65, 86–90 | Tech lead |
| 7 | QST-001's throw-only staging instruction omits inherited interface members and conflicts with the literal value constructors, metadata and property rule | Transcribe the 25 public types and internal QuestInstance. Preserve explicit enum values, readonly view constructors, initialized DTOs, Repeatable and immutable Order/QUST/version metadata; authored definition getters expose fields with read-only lists. Complete INarrativeSystem, ILogicNarrative, ISaveParticipant and the four declared ILogicSourceOwner signatures; operational bodies/event accessors throw their owning QST-002…007 IDs. ClueDefinition has no logic fields/owner interface per its §4.4 declaration and SPEC-40 §4.5; RumorDefinition adds the owner. Validator providers remain QST-002. No service composition, save registration, runtime behavior or content in QST-001. | Tech lead |
| 8 | The required quest resolver lines arrive while RefResolverRegistry and CatalogRefResolver are still NAR-006 shells | Stage the four typed registrations in CreateDefault: QuestDefinition/Quest, ClueDefinition/Clue (including rumors), StoryBeatDefinition/Beat, MysteryDefinition/Mystery. The existing generic resolver constructor, Register/TryGet and final CreateDefault path retain explicit NAR-006 throws. NAR-006 owns completing all families and returning the working registry; no partial runtime resolver or new API is claimed. | Tech lead |

| 9 | Whole SPEC-40 dependency prevents implementing quest definitions even though imported beats already require their validators at boot | QST-002 depends on completed contracts, compiler/binder, condition nodes, reference/fact catalog, import pipeline and validator framework: QST-001 and NAR-003/004/006/009/013. Runtime effect execution, narrative scheduling and campaign playthrough remain their own tasks; no bypass of local validation, fake bindings or reduced acceptance. | Tech lead |

| 10 | QST-002 local/cross-content validation boundaries and provider defaults | Definitions enforce applicable structural rules, database references and compiled-wrapper freshness/errors, with condition/expression root validation; compile/bind traverses all declared fields with the exact usages and semantic indexed paths. Clues have no logic owner and validate their optional mystery/link references. Editor providers implement the full rule table: Beat order15, Quest20, Mystery30; tag vocabulary is the specified union, objective IDs are unique across the quest, and static Jump/Detour edges determine node reachability. Command references use compiled graphs plus real compilation of command-table Text when that owner supplied no graphs; existing dialog-owner errors remain visible. Empty failWhen/resolvedWhen keep their canonical source semantics; runtime quest/clue services retain their specified empty=never guard. Imported version0 wrappers must be recompiled through the existing pipeline before boot acceptance. NAR-013's existing failed-owner case uses an explicit failing fixture owner after removing the quest shell; no test is removed or added outside the25 QST-required cases. No converter/content or runtime service implementation is included. | Tech lead — resolved; ledger Q348 |

| 11 | Quest/clue services wait on all SPEC-40, including debugger tools that need those same services | **Resolved:** under ADR-0068, QST-003/004 require QST-002 definitions/compiler/binder/validator plus NAR-005 effect execution, NAR-007 facts, NAR-010 ordered evaluation/dependency index and NAR-011 live context. These accepted prerequisites supply their complete local runtime; world-event routing, dialog, debugger UI and their integration acceptance retain their own tasks. No reduction of service behavior, restore semantics or tests. | Tech lead |
| 13 | Clue invalid indices, scope details, marker/QUST hooks and restore boundaries are abbreviated | Invalid index reads return false/0 and mutations return false/no-op; MarkSeen affects only known clues and is idempotent. Checked counter increments must remain positive. Mystery generated facts remain authoritative on restore: only a newly revealed required clue triggers the §5.4 completion path; latch the solved fact before effects, retaining it if preflight rejects, as the specified ordering requires. QuestEffect clears quest/instance/beat and carries the caller's optional body, restoring the prior scope in finally. Build the canonical DependencyIndex once over all rumors and filter known/latching at evaluation; include each rumor's generated clue fact so fact-driven visibility changes are reevaluated. The first evaluation after OnRestored is full and silent. Internal CaptureState/RestoreState exchange sorted seen/resolved ContentIds and preserve unknown IDs without notifications or fact/effect writes; OnNewGame clears them, OnRestored preserves them. An internal dirty flag/consume hook defers actual marker composition to QST-006; no QUST serialization or service registration is implemented here. Q12 covers QuestService. | Tech lead |

| 12 | Quest restore prose says re-evaluate but also preserve saved bits; effect callbacks can complete/restart a quest during progression | **Resolved:** decision 6 and QST-003 acceptance take precedence: the initial restored evaluation may read conditions but never changes saved bits/stages/status or applies effects, including failWhen. Subsequent dependency changes use normal progression. Evaluate canonical quest-index order and capture baselines before acceptance effects and after stage effects. Terminal status is set before effects; after callbacks, continue only while the same latest instance remains Active, so nested completion/repeatable starts cannot advance an old instance. Unknown queries return the declared empty defaults; missing baselines log and return current fact value. QUST importing/unknown IDs remain QST-007 and live adapter/registration remain QST-006/008. | Tech lead |

## 11. References

- Audit (background only): `docs/reference/audit-2026-10/05-narrative-quests.md` §2 (B3, B4, B5, B7, B8, B9, B10, B15), §5.5–§5.7 (clue graph, storylets, declarative quests).
- SPEC-40 §4.2 (generated families), §4.3 (`ILogicNarrative`), §5.3 (tick), §5.6 (bridge), §5.8 (converter, flag table, quest map); SPEC-41 §4.7 and §5.3 (dialog lifecycle); legacy files `Import/Legacy/narrative/*.json` and `Import/Legacy/content/Galaxies/Solar System Entities/**`.

| 14 | Opening content depends on whole galaxy/narrative specs, including its own downstream integrations | Use accepted QST-002, GAL-003, NAR-013, DLG-003, ENC-002 and ECO-002 definitions/compilers/validators under ADR-0068. Existing GAL-012 authored content is input, not a runtime acceptance prerequisite for authoring. Preserve every content validation and tag-coverage criterion; production opening playthrough stays QST-010. | Tech lead — resolved; ledger Q424 |

| 15 | Beat queue overflow without a Condition entry, recursive completion and concrete scheduler defaults | Keep the queue bounded at32: remove the oldest Condition entry on overflow; if none exists, warn and refuse the new request without losing an existing non-Condition entry. Deduplicate automatic Condition requests, not explicit queue effects. Suppress recursive same-beat completion while its effects execute; explicit completion of the current beat counts immediately and latches to avoid a second count at Ended. Use current authored-system index plus the supplied resolver for location filtering; body scopes carry the exact system/key. Internal per-body records and queue remain owned by BeatService for QST-007 serialization, with OnRestored preserving them and no effects during restoration. QST-006 owns ship-log forwarding; QST-008 owns production construction after the presenter. Advisory Select40 lives alongside Idle in NarrativeSystemsPerfTests so focused functional runs exclude it. | Tech lead — resolved; ledger Q476 |
