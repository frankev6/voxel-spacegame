using System.Text;
using Game.Data.Logic;

namespace Game.Editor.Narrative.Dialog
{
    public static class FactsYarnGenerator
    {
        public static string Build(FactCatalog facts)
        {
            var text = new StringBuilder();
            for (int slot = 0; slot < facts.Count; slot++)
                if (facts.FamilyAt(slot) == FactFamily.Authored)
                    text.Append("<<declare $").Append(facts.IdAt(slot).Name).Append(" = ")
                        .Append(facts.KindAt(slot) == FactKind.Bool ? "false" : "0").Append(">>\n");
            return text.ToString();
        }
    }
}
