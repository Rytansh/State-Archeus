# Stage snapshot reversion

This feature is based on `mvp_polish` at `60fd94d3ee16e0095d8f0325794dd7cca1d87776`.
The supplied **State: Archeus** design document, “Turns and Stages” and “Retreats”,
defines optional hand replenishment and a shared four-AP default budget per turn.
Drawing and planning have separate baselines; planning reversion preserves committed
drawing changes and does not grant more AP. This implements whole-stage reversion,
not action-by-action undo or a persistent save format.

## Integration and timing

`BattleStageSnapshotSystem` runs in `BattleRootGroup` after simulation and
`BattlePhaseTransitionSystem`, before presentation exports. Event processing now
explicitly follows turn-flow systems. The first Drawing baseline contains the
completed turn-start resource reset and passive effects. It precedes the optional
`DrawCardsRequest`; reverting and replenishing again gives the same cards because
both RNG words and the deck order are restored. There is no automatic drawing-entry
replenish effect in the current repository. Future drawing-entry effects must run
before this capture barrier, not on every frame or on restore.

On Drawing -> Planning, the drawing baseline is released and a planning baseline
is captured after committed drawing effects have resolved. Capture waits while
event, chained-event, or behaviour queues still contain entry work. Inputs are
disabled until a valid baseline exists; capture failures log an error and fail closed.
Planning consumes `EndPlanningRequest` in Planning (fixing the existing drawing-request
copy/paste bug). Queued attacks execute only in Attacking, after planning commits.
Attacking completion waits for committed actions and causal event/behaviour work
to drain, so the phase cannot advance after only the first queued attack.

`StageSnapshotWindow` owns validity and revisions independently of Unity. A baseline
is valid only for its battle ID, active player ID, turn and provisional stage.
Completion tags immediately reject restores even before the transition system runs.
Leaving either stage discards its snapshot. Every successful revert advances the
input revision while retaining the same immutable baseline.

The existing bootstraps register both snapshot systems and the presentation reset
system. UI code can use the existing gateway:

```csharp
RunBootstrap.InputGateway.RequestRevertStage(battleID, activeSide);
RunBootstrap.InputGateway.RequestEndDrawing(battleID, activeSide);
RunBootstrap.InputGateway.RequestEndPlanning(battleID, activeSide);
```

Revert takes priority over other valid commands for that battle in the same input
batch. An opponent's invalid revert cannot suppress the active player's commands.
The gateway stamps the revision at enqueue time. Routed transient requests carry
`StageCommandOwner`, giving future request types automatic cancellation and stale
revision rejection. A revert also cancels the existing legacy request types that
were created directly rather than through the gateway. New production input must
go through the gateway and attach `StageCommandOwner` if it creates transient ECS
request entities. Do not keep unplayed command buffers across this barrier.

## Captured state and structural changes

`BattleStateSnapshot` discovers the battle entity, every `OwnedBattle` member
(including disabled/prefab entities), validating their complete entity-reference graph.
All supported unmanaged components, tags, enable bits and dynamic buffer entries
are captured, including HP, energy, AP, card ownership/zones, stats, effects and
durations, passive memory, selections, full RNG state, execution queues, event
counters, action IDs, turn state and pending gameplay work. No per-character or
per-ability rollback code is necessary.

Every mutable runtime entity created during a battle must retain `OwnedBattle`
for its entire lifetime, even while absent from a zone. This is already done by
`CardEntityBuilder`. It lets restore discover and remove provisional entities that
are no longer referenced. Cross-battle mutable references and mutable references to
entities without ownership are rejected at capture, rather than accidentally
absorbing a shared singleton or another world service into the rollback scope.
Restore also rejects provisional references that escape this ownership boundary
before changing the world. A restore failure invalidates the stage's input fence
and is surfaced as an error instead of treating a partial/unsafe revert as success.

Restoration completes tracked jobs and validates the current schema before
changing entities. Surviving handles are retained; destroyed members are recreated.
It establishes all destination archetypes first, then restores values and buffers,
remapping every nested `Entity` field to its restored destination. Added components
are removed and removed components reintroduced. A second revert never mutates the
stored baseline. Battle/player/card **logical IDs remain stable**; raw handles to
destroyed entities cannot be resurrected by the public EntityManager API. External
consumers must resolve logical IDs after reversion instead of caching those handles.
The input inbox and presentation bridge are outside the authoritative snapshot.

The narrow, obsolete whole-world `CopyAndReplaceEntitiesFrom` API is deliberately
not used: this world contains scene-streaming infrastructure and managed bridge
references, and the API documents incomplete support for managed/cleanup state.

Immutable content blob references are retained without copying their data. The
content owner must keep blobs alive through the battle and all snapshot lifetimes.
Managed/shared/cleanup components, raw native allocations and entity-valued fixed
lists fail closed until a dedicated ownership/remapping codec is provided. Inline
numeric fixed lists, including passive `FixedList64Bytes<float>` memory, are copied
fully; the diagnostic hash enumerates their complete logical contents.

Future components are discovered automatically. Run:

```sh
python3 Tools/generate_snapshot_catalog.py
python3 Tools/generate_snapshot_catalog.py --check
```

The catalog roots closed generic codecs for the project's value components and
buffers; an Edit Mode reflection audit checks coverage. The linker descriptor
preserves GameAssembly metadata for private-field traversal. This trades some
IL2CPP stripping opportunities for completeness. IL2CPP execution remains unverified.
External package component types added to runtime entities also need appropriate
closed-codec roots and reflection metadata; unsupported ownership must never be
silently skipped.

Because entity recreation can change ECS chunk order, passive ordering now uses
`CardRuntimeID` instead of `Entity.Index`, and effect-expiration events use stable
card identity order. Passive collection is also scoped to its owning battle.
Current behaviour/effect holders are runtime cards and must have a `CardRuntimeID`.
Other future causal query loops must likewise use stable identity ordering.

`BaselineHash`/`ComputeCurrentHash` compare all supported values and entity topology
in that snapshot's logical identity space. They are diagnostic, in-process hashes,
not portable persistence/network hashes. Blob identity and the baseline identity
mapping are intentionally local to this process.

## Presentation and external effects

Transport revisions, presentation sequences, publication tags and pending visual
facts are operational state and are excluded from the gameplay baseline. Revision
counters remain monotonic, preventing restored state from being rejected as stale.
Pending abandoned facts/deltas are removed; the exporter cache is invalidated and
the existing authoritative state-recovery exporter publishes a fresh full projection.

Before presentation imports/executes another frame, `PresentationStageReversionSystem`
clears imported abandoned facts, action packets and execution plans. The assembly
system also prunes destroyed packet cache entries before reusing rewound action IDs.
The timeline executor clears buffered impacts and completion state. `CancelPlayback` stops the
director without firing plan completion/fallback damage, clears bindings and
temporary damage numbers, and ignores late notifications while no plan is active.
The current battle's animator bindings are reset. The full-snapshot importer resets
visible HP/alive state and existing hand/field/AP binding systems reconcile the UI.
Presentation reset never emits a gameplay event or invokes passive resolution.

The presentation architecture currently models one active battle mirror per
presentation world; this feature retains that convention. Registry resets follow
that active mirror, while transport/packet invalidation is battle-scoped. New VFX
owners must join cancellation instead of independently replaying abandoned effects.

No gameplay networking, rewards, persistence or telemetry sink was found in the
current simulation. Existing diagnostic logs remain in logs after rollback. Future
irreversible sinks must defer until stage commitment; they must not subscribe
directly to provisional visual facts as if they were committed gameplay events.

## Cost and limits

Snapshot storage is managed and proportional to the battle's component/buffer
contents, with one active baseline per provisional battle. Commit/end releases
references for GC; there is no retained native snapshot allocation. Reflection
metadata and codec factories are cached. Capture and restore intentionally synchronize
tracked jobs. Restore uses temporary record copies during schema validation and
boxes nested values for remapping. This favors correctness at infrequent stage
boundaries over zero-allocation, per-frame rollback. Generated field visitors and
pooled buffers are possible later optimizations; do not replace complete capture
with a hand-maintained list of visible stats.

The entire battle state is provisional: this implementation assumes the existing
single active-turn-player model, not concurrent remote opponents committing changes
to the same battle. Runtime entity destruction/recreation must not bypass ownership.
Systems must not retain authoritative state in mutable managed caches or unplayed
ECBs outside components. Restoration is not an out-of-memory recovery transaction;
unexpected failures after structural mutation must be treated as fatal, rather than
continuing the battle as though restoration succeeded.

Existing gameplay placeholders (planning action UI, transformations, retreat cleanup,
automatic supercharged-energy gain and other not-yet-implemented design mechanics)
are not implemented by this feature. Their eventual component/buffer state will be
captured under the same rules. Local Unity validation is required before calling
this implementation production-ready.

## Validation and running locally

Cloud Unity is installed but unlicensed. No Unity compilation, Edit Mode suite,
Play Mode suite, player build, IL2CPP build or visual smoke test has executed here.
The independent .NET runner executes the **same** lifecycle and deterministic RNG
test sources used by the Edit Mode assembly, without stubs for ECS:

```sh
dotnet run --project Tools/StageSnapshotValidation/StageSnapshotValidation.csproj -- \
  --work=/tmp/archeus-snapshot-tests --result=/tmp/archeus-snapshot-tests/result.xml
dotnet run --project Tools/StageSnapshotValidation/StageSnapshotValidation.csproj -- \
  --syntax-root "$PWD"
python3 Tools/generate_snapshot_catalog.py --check
python3 Tools/validate_snapshot_assets.py
git diff --check
```

The syntax check parses C# 9; it does **not** resolve Unity APIs or run source
generators. The .NET runner covers lifecycle/fencing and full RNG continuation only,
not ECS copying or presentation. See the PR for actual executed results.

On a locally activated **Unity 6000.0.23f1**:

1. Open this feature branch's project, let Package Manager resolve the checked-in
   manifest and lockfile, and wait for compilation. Keep the pinned Entities 1.3.14.
2. Open **Window > General > Test Runner**. In EditMode, run the namespace
   `Archeus.Testing.Battle.Snapshots`, then the full existing EditMode suite. Run
   PlayMode `PresentationCancellationTests` as well.
3. For command-line execution, set `UNITY_EDITOR` to your local executable and
   choose an absolute output directory outside Assets:

```sh
OUT=/tmp/archeus-unity-results
mkdir -p "$OUT"
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests \
  -testPlatform EditMode -testFilter Archeus.Testing.Battle.Snapshots \
  -testResults "$OUT/snapshot-editmode.xml" -logFile "$OUT/snapshot-editmode.log"
"$UNITY_EDITOR" -batchmode -projectPath "$PWD" -runTests \
  -testPlatform PlayMode -testFilter Archeus.Testing.Battle.Snapshots \
  -testResults "$OUT/snapshot-playmode.xml" -logFile "$OUT/snapshot-playmode.log"
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests \
  -testPlatform EditMode -testResults "$OUT/all-editmode.xml" -logFile "$OUT/all-editmode.log"
```

Do not add `-quit` to Test Framework invocations; the runner controls completion.
Check the process exit code, fresh result XML test counts and logs. Do not accept
a zero-test run. Keep the Unity instances sequential because they share a project.

The new Edit Mode coverage includes resources, effects/DOT/passive memory, a real
VM-triggered passive and effect replay, RNG state, event/behaviour/action queues,
multiple drawing actions, planning commit isolation, stale/opponent input, repeated
restoration, component additions/removals, destroyed/created entities, enable bits,
unsupported schema rejection, stable event ordering, AOT catalog coverage and
presentation transport/ECS invalidation. Play Mode tests exercise timeline cancellation
without completion/late impacts and temporary-number cleanup.

Finally smoke-test BootstrapScene -> battle: replenish, place, retreat and revert;
check AP, card zones and selections; commit drawing; make/revert planning changes;
verify committed drawing persists, then commit planning. Watch for stale timeline
impacts and verify no gameplay event/passive executes merely because of restore.
Return Unity compiler diagnostics, failed XML cases and relevant runtime logs for fixes.
