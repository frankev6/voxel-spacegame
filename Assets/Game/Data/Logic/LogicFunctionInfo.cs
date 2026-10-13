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

        // Arguments retain typed expression nodes and canonical names until binding.
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
        /// <summary>SPEC-41 function boundary: args contain evaluated values, bound indices/slots and retained canonical IDs.</summary>
        public LogicValue Invoke(ILogicContext context, System.ReadOnlySpan<LogicValue> args)
        {
            if (context == null) throw new System.ArgumentNullException(nameof(context));
            if (IsEffect) throw new System.InvalidOperationException("Commands must use compiled effects and their preflight/flow handling.");
            if (args.Length != Params.Length) throw new System.ArgumentException("Supply all parameters, including defaults.", nameof(args));
            if (context.Scope.Usage == LogicUsage.None || (AllowedUsage & context.Scope.Usage) != context.Scope.Usage)
                throw new System.InvalidOperationException("Function is not allowed in the current logic scope: " + Name);
            int value;
            switch (Name)
            {
                case "has_item": value = context.Economy.GetItemCount(args[0].Int) >= args[1].Int ? 1 : 0; break;
                case "item_count": value = context.Economy.GetItemCount(args[0].Int); break;
                case "credits": value = (int)System.Math.Clamp(context.Economy.GetCredits(), 0L, int.MaxValue); break;
                case "quest_not_started": value = context.Narrative.GetQuestStatus(args[0].Int) == QuestStatus.NotStarted ? 1 : 0; break;
                case "quest_active": value = context.Narrative.GetQuestStatus(args[0].Int) == QuestStatus.Active ? 1 : 0; break;
                case "quest_completed": value = context.Narrative.GetQuestStatus(args[0].Int) == QuestStatus.Completed ? 1 : 0; break;
                case "quest_failed": value = context.Narrative.GetQuestStatus(args[0].Int) == QuestStatus.Failed ? 1 : 0; break;
                case "quest_stage": value = context.Narrative.GetQuestStage(args[0].Int); break;
                case "objective_done": value = context.Narrative.IsObjectiveDone(args[0].Int, args[1].Text) ? 1 : 0; break;
                case "clue_known": value = GeneratedFact(context, args[0].Text) > 0 ? 1 : 0; break;
                case "mystery_solved": value = GeneratedFact(context, args[0].Text) == 1 ? 1 : 0; break;
                case "beat_count": value = GeneratedFact(context, args[0].Text); break;
                case "since_accept": case "since_stage":
                    value = Saturate((long)context.Facts.GetBySlot(args[0].Int) -
                        context.Narrative.GetBaseline(context.Scope, args[0].Int, Name == "since_accept" ? BaselineKind.Accept : BaselineKind.Stage)); break;
                case "in_star_system": value = context.World.CurrentStarSystemIndex == args[0].Int ? 1 : 0; break;
                case "relation":
                    int faction = args[0].Text == "owner" ? context.World.CurrentStarSystemOwnerFactionIndex : args[0].Int;
                    value = faction == 0 ? 0 : context.World.GetRelation(faction); break;
                case "star_system_owned": value = context.World.CurrentStarSystemOwnerFactionIndex != 0 ? 1 : 0; break;
                case "game_hours": value = (int)System.Math.Clamp(System.Math.Floor(context.World.UniversalTime / 3600), int.MinValue, int.MaxValue); break;
                case "roll":
                    if (args[0].Int < 2 || args[0].Int > 1000) throw new System.ArgumentOutOfRangeException(nameof(args));
                    value = context.Narrative.Roll(args[0].Int); break;
                case "visited_count": value = context.Narrative.GetDialogVisitCount(args[0].Text); break;
                default: throw new System.InvalidOperationException("Unknown function: " + Name);
            }
            return new LogicValue(ReturnType, value);
        }
        static int Saturate(long value) => (int)System.Math.Clamp(value, int.MinValue, int.MaxValue);
        static int GeneratedFact(ILogicContext context, string canonicalId)
        {
            int slot = context.Facts.Catalog.SlotOf(canonicalId);
            if (slot < 0) throw new System.InvalidOperationException("Unknown generated fact: " + canonicalId);
            return context.Facts.GetBySlot(slot);
        }
    }
}
