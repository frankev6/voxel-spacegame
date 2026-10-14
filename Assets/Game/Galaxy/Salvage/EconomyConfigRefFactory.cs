using System;
using Game.Data.Definitions;
namespace Game.Galaxy.Salvage
{
    public static class EconomyConfigRefFactory
    {
        public static EconomyConfigRef Create(EconomyConfigDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return new EconomyConfigRef { SalvageDistanceM = definition.SalvageDistanceM };
        }
    }
}
