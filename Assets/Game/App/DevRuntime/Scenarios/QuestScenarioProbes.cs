#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Narrative.Clues;
using Game.Narrative.Dialog;
using Game.Narrative.Quests;
using Game.Presentation.Bridges;
using Game.Input;
using Game.UI.Services;
using static Game.App.DevRuntime.Scenarios.ScenarioExpression;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>QST-010: canonical quest status/clue ordinal and read-only dialogue input readiness.</summary>
    public static class QuestScenarioProbes
    {
        sealed class Probe : IScenarioProbe
        {
            public string Name { get; }
            public int ArgCount => Name == "save.allowed" ? 0 : 1;
            internal Probe(string name) { Name = name; }
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args)
            {
                if (Name == "save.allowed")
                {
                    if (!ctx.TryGetService<Game.Core.Flow.IGameFlow>(out var flow) ||
                        !ctx.TryGetService<Game.Persistence.Services.SaveService>(out var save))
                        throw new InvalidOperationException("production save/flow services unavailable");
                    return Number(!flow.IsTransitioning && !save.IsBusy && flow.HasActiveSession &&
                        flow.CurrentScene == Game.Core.Flow.GameScene.World &&
                        flow.GetSaveBlock() == Game.Core.Save.SaveBlockReason.None ? 1 : 0);
                }
                if (Name.StartsWith("dialog.", StringComparison.Ordinal))
                {
                    if (!ctx.TryGetService<DialogService>(out var dialog) || !ctx.TryGetService<DialogPresenter>(out var presenter))
                        throw new InvalidOperationException("production dialog service/presenter unavailable");
                    string text = RequireString(args[0]);
                    if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("dialog.option expects visible option text");
                    // A populated page can be covered before deferred docking/navigation settles.
                    // Advertise a number only when its actual input route can receive the press.
                    if (dialog.IsActive && presenter.Model.IsWaitingForChoice && presenter.View.IsOpen &&
                        ctx.TryGetService<UINavigator>(out var navigation) && ReferenceEquals(navigation.Top, presenter.View) &&
                        ctx.TryGetService<InputService>(out var input) && input.CurrentContext == InputContext.Dialog && input.Controls.Dialog.enabled)
                        foreach (var option in presenter.Model.Options)
                            if (option.Text == text && option.Enabled) return Number(option.Number);
                    return Number(0);
                }
                string id = RequireString(args[0]);
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(Name + " expects a content id");
                if (!ctx.TryGetService<ContentService>(out var content)) throw new InvalidOperationException("content service unavailable");
                if (Name == "star_system_id")
                {
                    if (!content.Catalog.TryGet<AuthoredStarSystemDefinition>(new ContentId(id), out var system)) throw new ArgumentException("unknown authored star system '" + id + "'");
                    return Number(StarSystemId.FromAuthored(system.Id.Hash).Value);
                }
                if (Name == "quest")
                {
                    if (!content.Catalog.TryGet<QuestDefinition>(new ContentId(id), out var definition)) throw new ArgumentException("unknown quest '" + id + "'");
                    if (!ctx.TryGetService<QuestService>(out var quests)) throw new InvalidOperationException("production QuestService unavailable (QST-008)");
                    return Text(quests.GetStatus(content.Catalog.IndexOf(definition)).ToString());
                }
                if (!content.Catalog.TryGet<ClueDefinition>(new ContentId(id), out var clue)) throw new ArgumentException("unknown clue '" + id + "'");
                if (!ctx.TryGetService<ClueService>(out var clues)) throw new InvalidOperationException("production ClueService unavailable (QST-008)");
                return Number(clues.OrdinalOf(content.Catalog.IndexOf<ClueDefinition>(clue)));
            }
        }
        internal static void Register(ScenarioProbeRegistry registry)
        {
            registry.Register(new Probe("quest")); registry.Register(new Probe("clue"));
            registry.Register(new Probe("dialog.option"));
            registry.Register(new Probe("star_system_id")); registry.Register(new Probe("save.allowed"));
        }
    }
}
#endif
