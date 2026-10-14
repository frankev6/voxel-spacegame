using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Economy;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Presentation.Presenters;
using Game.Simulation;
using Game.UI.Services;
using Game.UI.Views;

namespace Game.App.Economy
{
    /// <summary>SPEC-50 §5.8: merchant sessions use the encounter faction or current territory owner, then the real presenter.</summary>
    public sealed class MerchantLauncher
    {
        readonly EconomyService economy;
        readonly MerchantDirectory directory;
        readonly FactionService factions;
        readonly TerritoryService territory;
        readonly EncounterDirectorService encounters;
        readonly StarSystemLoader loader;
        readonly UINavigator navigator;
        public MerchantLauncher(EconomyService economy, MerchantDirectory directory, FactionService factions,
            TerritoryService territory, EncounterDirectorService encounters, StarSystemLoader loader, UINavigator navigator)
        {
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
            this.factions = factions ?? throw new ArgumentNullException(nameof(factions));
            this.territory = territory ?? throw new ArgumentNullException(nameof(territory));
            this.encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        }
        public bool TryOpenForBody(BodyId body, Action onClosed)
        {
            var merchant = directory.FindForBody(body);
            if (merchant == null) return false;
            Open(merchant, onClosed); return true;
        }
        public void Open(MerchantDefinition merchant, Action onClosed, ulong encounterMemberPersistentId = 0)
        {
            if (merchant == null) throw new ArgumentNullException(nameof(merchant));
            FactionDefinition faction = null;
            if (encounterMemberPersistentId != 0)
            {
                if (encounters.TryGetInstanceByMember(encounterMemberPersistentId, out var instance)) faction = instance.Definition?.Faction;
            }
            else faction = territory.GetOwner(loader.Loaded);
            int relation = faction == null ? 0 : factions.GetRelation(faction);
            bool refused = faction != null && factions.GetPlayerStance(faction) == FactionStance.Hostile;
            // Resolve UI readiness before replacing an existing economy session.
            var presenter = Game.Core.Services.Get<MerchantPresenter>(); navigator.Get<MerchantView>();
            var session = economy.Merchants.OpenSession(merchant, relation, refused, refused ? "They refuse to trade with you." : "");
            try { presenter.Show(session, onClosed); }
            catch { session.Close(); throw; }
        }
    }
}
