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
