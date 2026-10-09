# Integration fixes to apply during the review pass

1. SPEC-10 `VoxelStructureCodec` (Appendix A) duplicates SPEC-05 `VoxsCodec`. Make VoxelStructureCodec a thin adapter: VoxelStructure -> VoxsSource (+VoxsBlockNames) -> VoxsCodec.Encode; VoxsDecoded -> VoxelStructure. Delete Appendix A byte layout or mark it superseded by SPEC-05 §4.x.
2. PLOC (SPEC-31) saves CurrentStarSystem/PlayerLocation; GLXY (SPEC-30) does not. Done in SPEC-30 §5.11. SPEC-31 must implement PLOC on the same GalaxyState.
3. SPEC-13 PersistentIdAllocator matches the SPEC-02 type added now ({Next, End}). SPEC-01 must define the refill (block 4,096 when remaining < 1,024).
4. SPEC-10 §10 Q7: StarSystemMember for hulks: SPEC-31 adds a WorldGroup system that batch-adds the parent's StarSystemMember. SPEC-31 must include it.
5. SPEC-61: MainMenuPresenter uses IGameFlow.NewGameAsync(new NewGameArgs(name, 0, false, ContentId.None)); UiComposition implements Game.UI.Views.IRootScreenSet {MainMenu, FlightHud, ShipEditor, Loading}; defeat uses ConfirmDialog (SPEC-01 §5.9).
6. SPEC-70: `newgame` -> IGameFlow.NewGameAsync(NewGameArgs); `sandbox` -> SceneFlow.EnterSandboxAsync(SandboxArgs(spawnPlayer, seed)); ShipSpawnService.Spawn signature as SPEC-01 §4.4.
7. SPEC-05: delete its own SaveReason/SaveBlockReason (use Core's, SPEC-02 Q4); quit hook: SaveService subscribes Application.wantsToQuit itself (HandleWantsToQuit is internal) — make that explicit.
8. All specs: singletons owning native memory created in OnCreate; cleanup/disposal systems (DeferredDisposalSystem, chunk/debris cleanup, VFX/audio emitter cleanup) must NOT RequireForUpdate<GameplayActive> (SPEC-01 Decision 5). Check SPEC-10/11/12/13/63/64.
9. WorldSceneActive: streaming/encounters/travel/territory/OriginRebase systems require it (SPEC-31/32/33 when written).
10. SPEC-23: define PlayerCombatStatus singleton {ushort HostilesEngaging; uint LastDamagedTick} + its writer system; SPEC-13 source of "player damaged this tick".
11. SPEC-31: declare Game.Galaxy.Services.IShipSpawner (implemented by ShipSpawnService); spawn hook adds StarSystemMember to bricks/functional entities of non-player grids (SPEC-10 Q7).
12. SPEC-04: `config_` prefix registered for GameConfigDefinition (SPEC-13 Q11 also asks for it).
13. SPEC-61: OpenFleet hotkey is K (F = Interact) per SPEC-03 §4.2.
14. SPEC-01 §5.2: step 9.1 bind PlayerInputSampleSystem (INP-005); step 12 cast settings.Current.Video.VfxQuality (byte) to VfxQuality; step 4 audio volumes conversion name check vs SPEC-02 AudioSettingsData.
15. SPEC-20 must define ShipCommand/PlayerCommand/ShipCommandFlags/ShipCommandEdges/PlayerEdgeCounters exactly as SPEC-03 §4.4 lists, and PlayerCommandApplySystem consuming counters.
16. [VOX-001 part DONE: deps now CORE-001, DATA-001; VoxelSingletons in VOX-013 which depends on REN-001, PHY-001] Dependency cycles: VOX-001 must drop `REN-001`/`PHY-001` (move `VoxelSingletons` from VOX-001's file list to VOX-013, which depends on REN-001 and PHY-001). VOX-018 (integrated voxel gate suite) depends on SPEC-11/SPEC-12 while REN/PHY tasks depend on SPEC-10 → move VOX-018 into SPEC-71 as a TST task (remove the VOX-018 ledger row, which is still `todo`).
17. SPEC-10 §5.8 / §5.9 and VOX-013/VOX-015: access `ColliderDirtyQueue` per SPEC-12 §5.1 (`Acquire/Release` in jobs, `CompleteAccess` on the main thread), not `GetSingletonRW`. Same review for SPEC-11 `BrickMeshQueue` (GetSingletonRW in a fixed-step system completes other systems' jobs).
18. SPEC-10 VOX-013 (Convert): prefill `GridMass { Data = source.GetMass(content) }` and disable `GridMassRecompute` (SPEC-12 Q3). Root archetype lists `GridMass, GridMassRecompute, GridColliderState` (already there).
19. SPEC-13: SplitSystem hulk creation uses `GridBodyFactory` (SPEC-12 §5.6): DynamicBodyTypes + provisional collider + PhysicsWorldIndex; chunk colliders use `PhysicsLayers.ChunkMaterial`; §4.9 brick transform scale is `CellSize` (SPEC-10 §4.5), not 1; `CellRemovedView` arrays are deferred job arrays (SPEC-12 Q8); SPEC-13 defines its own `GridDespawnRequest` (request entity) which collides with SPEC-10's enableable `GridDespawnRequest` — rename SPEC-13's to `GridDissolveRequest` or reuse SPEC-10's.
20. Architecture §8: PhysicsVelocity row adds GridMassUpdateSystem (exact re-expression only); creation of PhysicsCollider/PhysicsMass/PhysicsVelocity by GridBodyInitSystem, SplitSystem (via GridBodyFactory) is not a write (SPEC-12 Q2, SPEC-13 Q14). Also `VoxelGrid.Kind` writer ShipDeathSystem (SPEC-13 Q6).
21. SPEC-01 §5.3 BootSingletons: rows `PhysicsVoxelConfig` = `PhysicsVoxelConfigFactory.Create(catalog.Get<PhysicsVoxelConfigDefinition>("config_physics"))` and `PhysicsStep` = `PhysicsVoxelConfigFactory.CreatePhysicsStep(same def)`.
22. SPEC-10 §8.2 memory table collider line: use SPEC-12 §4.8 (≈ 2.3 MB peak, 1.1 MB steady for HollowStation1M).
23. SPEC-01 §5.7 step 4: call `FlightSetup.Add(em, root)` (SPEC-20) next to `PlanarConstraintSetup.Add`; BootSingletons row `FlightConfig` = `FlightConfigFactory.Create(catalog.Get<FlightConfigDefinition>("config_flight"))`.
24. SPEC-22: `ShipStats.YawTorque` += `FlightMath.GyroYawTorque(FlightConfig, InverseInertia.y)` for non-hulk ships (mass part, §5.4 step 4); PWR-006 depends on FLT-001 for `GridSpaceMath` (not WPN-001); §10 Q5 answered (SPEC-20 FLT-012).
25. SPEC-31 InteractionQuery/interaction: consume `PlayerCommand.Edges.Interact` with its own last-seen counter (SPEC-20 §6), never `ShipCommand.Edges` (variable-rate group).
26. SPEC-23: AI steering writes `ShipCommand` with `WithNone<PlayerShip>` and rewrites `Edges` every tick; uses `FlightState`/`FlightMath` caps.
27. Architecture §8 PhysicsVelocity row: add `PlanarConstraintSystem` (vertical impulses only) — together with item 20.
28. SPEC-21: `WeaponAimSystem` declares `[UpdateAfter(typeof(PlanarConstraintSystem))]`; uses `GridSpaceMath` from FLT-001.
29. [DONE] SPEC-22 PWR-008 depends on WPN-001 (not SPEC-21) to break the WPN-012 ↔ PWR-008 cycle; its validator reads `WeaponDefinition.powerDraw` from the SPEC-72 assets.
30. SPEC-22: `ShieldHitStreams` registration API (`BeginWrite(ref state, ShieldHitProducer, forEachCount)`, `EndWrite(producer, handle)`, consumer combines handles) instead of main-thread field writes with GetSingletonRW (SPEC-21 Q3).
31. SPEC-63 VFX-006/§5.3/§6.5: tracers read `ProjectileStore` rows (SPEC-21 §4.6) instead of `Projectile` entities; `ProjectileDead` no longer exists.
32. SPEC-30 §5.10 ShiftJob chain: call `ProjectileStore.ScheduleShift(delta, starSystemChanged)` (SPEC-21 Q5).
33. SPEC-03 §5.2 step 7: aim = PickingQuery raycast through the cursor (exclude the player's grid), plane Y=0 fallback (SPEC-21 Q6); update decision 5.
34. SPEC-63 §6.x / SPEC-64 §6.2 producer pattern: `GetSingleton` (RO) + `[NativeDisableContainerSafetyRestriction]` ParallelWriter; drains use GetSingletonRW + CompleteDependency (SPEC-21 Q7).
35. SPEC-72 MIG-009: write SPEC-21 §4.9 values; damage type into `WeaponDamageType`.
36. SPEC-01 BootSingletons: `WeaponContent.CreateSingletons(em, catalog, created)` (WeaponDefsRef, WeaponTuning, default FactionRelations); `CombatGridSetup.AddAll` signature as SPEC-21 §4.3.
37. SPEC-61 Q2/Q4 satisfied by SPEC-21 `WeaponState`, `ShipTarget`.
38. SPEC-02 + SPEC-30: add Core singleton `OriginShiftRecord { uint Sequence; float3 Delta; byte StarSystemChanged; }` (Game.Core.Components), created in `OriginRebaseSystem.OnCreate`, written by `DecideJob` (Sequence = FrameOrigin.ShiftSequence) — consumers: SPEC-21 ProjectileStore, SPEC-23 AI anchors.
39. SPEC-13 Q9: drop `SpatialHashBuildSystem [UpdateBefore(ImpactEventSystem)]` — it is `OrderFirst` in GameplayPostPhysicsGroup (SPEC-23 §5.0).
40. SPEC-22 PowerAllocationSystem demand: a weapon also requests while `WeaponControl.Autofire == 1` and `ShipTarget.Value != Null` (SPEC-23 Q2).
41. Architecture §5 comment "ShipProxy hash" → "SpatialProxy hash"; SPEC-70 §(overlay) reads `SpatialProxy` (SPEC-23 Q5).
42. SPEC-63 tracers: hide/correct while `ProjectileStore.AppliedShiftSequence != FrameOrigin.ShiftSequence`.
43. SPEC-01 BootSingletons: `AiContent.CreateSingletons`; `AiSetup.Add(em, root, profile)` after `CombatGridSetup.AddAll`; save blocker reads `PlayerCombatStatus` with `CompleteDependencyBeforeRO`.
44. SPEC-31: add `SpatialBody { Radius }` to celestial body roots that block shots.
