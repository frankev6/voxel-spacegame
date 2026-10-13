namespace Game.Data.Logic
{
    /// <summary>Function metadata and the distinct node-construction/runtime invocation boundaries (SPEC-40 §4.7, Q12).</summary>
    public sealed class LogicFunctionInfo
    {
        public string Name;
        public bool IsEffect;
        public LogicType ReturnType;
        public LogicParamInfo[] Params = System.Array.Empty<LogicParamInfo>();
        public LogicUsage AllowedUsage;
        public EffectFlow Flow;
        public int Milestone;

        // TODO(NAR-003): construct the selected ICondition/IIntExpr/IEffect while retaining bound nodes, refs and literals.
        public object CreateNode(System.ReadOnlySpan<object> boundArgs)
        {
            if (boundArgs.Length != Params.Length) throw new System.ArgumentException("Supply all parameters, including defaults.", nameof(boundArgs));
            if (IsEffect)
            {
                switch (Name)
                {
                    case "set_fact": return new Nodes.SetFactEffect(boundArgs);
                    case "add_fact": return new Nodes.AddFactEffect(boundArgs);
                    case "give_item": return new Nodes.GiveItemEffect(boundArgs);
                    case "take_item": return new Nodes.TakeItemEffect(boundArgs);
                    case "give_credits": return new Nodes.GiveCreditsEffect(boundArgs);
                    case "take_credits": return new Nodes.TakeCreditsEffect(boundArgs);
                    case "give_loot": return new Nodes.GiveLootEffect(boundArgs);
                    case "give_ship": return new Nodes.GiveShipEffect(boundArgs);
                    case "start_quest": return new Nodes.StartQuestEffect(boundArgs);
                    case "fail_quest": return new Nodes.FailQuestEffect(boundArgs);
                    case "complete_quest": return new Nodes.CompleteQuestEffect(boundArgs);
                    case "track_quest": return new Nodes.TrackQuestEffect(boundArgs);
                    case "quest_log": return new Nodes.QuestLogEffect(boundArgs);
                    case "reveal_clue": return new Nodes.RevealClueEffect(boundArgs);
                    case "queue_beat": return new Nodes.QueueBeatEffect(boundArgs);
                    case "complete_beat": return new Nodes.CompleteBeatEffect(boundArgs);
                    case "abort_beat": return new Nodes.AbortBeatEffect(boundArgs);
                    case "spawn_encounter": return new Nodes.SpawnEncounterEffect(boundArgs);
                    case "start_combat": return new Nodes.StartCombatEffect(boundArgs);
                    case "remove_body": return new Nodes.RemoveBodyEffect(boundArgs);
                    case "replace_body": return new Nodes.ReplaceBodyEffect(boundArgs);
                    case "request_lane_jump": return new Nodes.RequestLaneJumpEffect(boundArgs);
                    case "open_merchant": return new Nodes.OpenMerchantEffect(boundArgs);
                    case "open_ship_editor": return new Nodes.OpenShipEditorEffect(boundArgs);
                    case "repair_ship": return new Nodes.RepairShipEffect(boundArgs);
                    case "damage_ship": return new Nodes.DamageShipEffect(boundArgs);
                    case "adjust_relation": return new Nodes.AdjustRelationEffect(boundArgs);
                    case "play_sfx": return new Nodes.PlaySfxEffect(boundArgs);
                    case "notify": return new Nodes.NotifyEffect(boundArgs);
                    default: throw new System.InvalidOperationException("Unknown effect: " + Name);
                }
            }
            return ReturnType == LogicType.Bool
                ? (object)new Nodes.BoolFunctionCondition(this, boundArgs)
                : new Nodes.IntFunctionExpr(this, boundArgs);
        }
        // TODO(NAR-003): invoke the vocabulary entry with runtime values (Yarn boundary).
        public LogicValue Invoke(ILogicContext context, System.ReadOnlySpan<LogicValue> args) => throw new System.NotImplementedException("NAR-003");
    }
}
