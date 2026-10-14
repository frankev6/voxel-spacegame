using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
    public sealed class DialogView : UIScreen
    {
        readonly DialogViewModel viewModel;
        readonly IDialogCommands commands;

        public DialogView(DialogViewModel vm, IDialogCommands commands)
        {
            viewModel = vm;
            this.commands = commands;
        }

        public override string Key => "dialog";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Dialog;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        public override bool OnBack() => commands.Back();
        // TODO(DLG-009): wire authored elements, synthetic UXML labels and Dialog input actions.
        protected override void OnBuild() { }
    }
}
