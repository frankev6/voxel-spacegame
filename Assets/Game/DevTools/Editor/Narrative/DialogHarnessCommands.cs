using System.Globalization;
using System.Text;
using Game.App.DevRuntime;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Narrative.Dialog;
using Game.Presentation.Bridges;
using UnityEditor;
using Unity.Pipeline.Commands;

namespace Game.DevTools.Editor.Narrative
{
    /// <summary>SPEC-41 DLG-012: inspect the displayed page and choose through the same presenter as the UI.</summary>
    public static class DialogHarnessCommands
    {
        [CliCommand("gh_dialog", "Read active dialog beat, node, page and numbered options as JSON (play mode).")]
        public static string Dialog()
        {
            if (!EditorApplication.isPlaying) return Snapshot(null, null, null, "not playing");
            Services.TryGet<DialogService>(out var service); Services.TryGet<DialogPresenter>(out var presenter);
            Services.TryGet<ContentService>(out var content);
            return Snapshot(service, presenter, content?.Catalog);
        }
        [CliCommand("gh_dialog_choose", "Choose a displayed dialog option by its UI number (1..9, play mode).")]
        public static string DialogChoose([CliArg("n", "Displayed option number, 1..9")] int n)
        {
            if (!EditorApplication.isPlaying) return "failed: not playing";
            Services.TryGet<DialogService>(out var service); Services.TryGet<DialogPresenter>(out var presenter);
            return Choose(service, presenter, n);
        }
        // Public pure/explicit-owner seams match existing Editor harness tests without changing assembly references.
        public static string Snapshot(DialogService service, DialogPresenter presenter, ContentCatalog content = null, string unavailableReason = null)
        {
            bool available = unavailableReason == null && service != null && presenter != null;
            string reason = unavailableReason ?? (service == null ? "dialog service unavailable (production runner not installed)" :
                presenter == null ? "dialog presenter unavailable" : "");
            bool active = service != null && service.IsActive;
            int beat = active ? service.CurrentBeatIndex : 0;
            string beatId = beat == 0 ? "" : content != null && beat > 0 && beat <= ushort.MaxValue
                ? content.GetByIndex<StoryBeatDefinition>((ushort)beat)?.IdString : null;
            var text = new StringBuilder("{\"available\":").Append(available ? "true" : "false")
                .Append(",\"reason\":").Append(DevJsonWriter.Quote(reason)).Append(",\"active\":").Append(active ? "true" : "false")
                .Append(",\"beatIndex\":").Append(beat.ToString(CultureInfo.InvariantCulture)).Append(",\"beat\":").Append(DevJsonWriter.Quote(beatId))
                .Append(",\"node\":").Append(DevJsonWriter.Quote(active ? service.CurrentNode : ""))
                .Append(",\"waitingForChoice\":").Append(active && service.CanSelectOption ? "true" : "false")
                .Append(",\"canContinue\":").Append(active && service.CanAcknowledgeEnd ? "true" : "false").Append(",\"lines\":[");
            if (available && active)
                for (int i = 0; i < presenter.Model.Lines.Count; i++)
                {
                    var line = presenter.Model.Lines[i]; if (i != 0) text.Append(',');
                    text.Append("{\"text\":").Append(DevJsonWriter.Quote(line.Text)).Append(",\"speaker\":").Append(DevJsonWriter.Quote(line.SpeakerName))
                        .Append(",\"draft\":").Append(line.IsDraft ? "true" : "false").Append('}');
                }
            text.Append("],\"options\":[");
            if (available && active)
                for (int i = 0; i < presenter.Model.Options.Count; i++)
                {
                    var option = presenter.Model.Options[i]; if (i != 0) text.Append(',');
                    text.Append("{\"number\":").Append(option.Number.ToString(CultureInfo.InvariantCulture))
                        .Append(",\"numberLabel\":").Append(DevJsonWriter.Quote(option.NumberLabel))
                        .Append(",\"id\":").Append(option.OptionId.ToString(CultureInfo.InvariantCulture)).Append(",\"text\":").Append(DevJsonWriter.Quote(option.Text))
                        .Append(",\"available\":").Append(option.IsAvailable ? "true" : "false").Append(",\"quest\":").Append(option.IsQuest ? "true" : "false")
                        .Append(",\"synthetic\":").Append(option.Synthetic.ToString(CultureInfo.InvariantCulture)).Append('}');
                }
            return text.Append("]}").ToString();
        }
        public static string Choose(DialogService service, DialogPresenter presenter, int number)
        {
            if (service == null) return "failed: dialog service unavailable (production runner not installed)";
            if (presenter == null) return "failed: dialog presenter unavailable";
            if (!service.IsActive) return "failed: no active dialog";
            if (number < 1 || number > 9) return "failed: n must be between 1 and 9";
            foreach (var option in presenter.Model.Options)
            {
                if (option.Number != number) continue;
                if (!option.IsAvailable) return "failed: option is unavailable";
                bool ready = option.Synthetic == 2 ? service.CanAcknowledgeEnd : service.CanSelectOption;
                if (!ready) return "failed: dialog is not waiting for this choice";
                presenter.ChooseByNumber(number);
                return "accepted: " + number.ToString(CultureInfo.InvariantCulture);
            }
            return "failed: numbered option not found";
        }
    }
}
