using Serilog;
using System.ComponentModel;
using System.Windows;

namespace kellerkompanie_sync
{
    public enum AddonGroupState
    {
        Unknown,
        NonExistent,
        Partial,
        CompleteButNotSubscribed,
        NeedsUpdate,
        Ready
    }

    public class AddonGroup : INotifyPropertyChanged
    {
        public WebAddonGroupBase WebAddonGroupBase { get; set; }
        public string Icon { get; set; }
        public string IconTooltip { get; set; }
        public string IconColor { get; set; }
        public string ButtonText { get; set; }
        public bool ButtonIsEnabled { get; set; }
        public Visibility ButtonVisibility { get; set; }
        public Visibility CheckBoxVisibility { get; set; }
        public bool CheckBoxIsSelected { get; set; }
        public string StatusText { get; set; }
        public Visibility StatusVisibility { get; set; }
        public AddonGroupState State { get; set; }

        public AddonGroup(WebAddonGroupBase WebAddonGroupBase)
        {
            this.WebAddonGroupBase = WebAddonGroupBase;
            SetState(AddonGroupState.Unknown);
        }

        private const string Green = "#5cb85c";
        private const string Red = "#d9534f";
        private const string Yellow = "#f7c516";

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetState(AddonGroupState newState)
        {
            Log.Information("setting " + WebAddonGroupBase.Name + " to " + newState);
            State = newState;
            switch (newState)
            {
                case AddonGroupState.Unknown:
                    Icon = "/Images/questionmark.png";
                    IconTooltip = "Unknown";
                    IconColor = Red;

                    CheckBoxVisibility = Visibility.Hidden;

                    ButtonVisibility = Visibility.Hidden;
                    ButtonText = "";
                    ButtonIsEnabled = false;
                    break;

                case AddonGroupState.NonExistent:
                    Icon = "/Images/link.png";
                    IconTooltip = "All mods missing";
                    IconColor = Red;

                    CheckBoxVisibility = Visibility.Hidden;

                    ButtonVisibility = Visibility.Visible;
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.CompleteButNotSubscribed:
                    Icon = "/Images/link.png";
                    IconTooltip = "All mods downloaded, but not subscribed";
                    IconColor = Green;

                    CheckBoxVisibility = Visibility.Hidden;

                    ButtonVisibility = Visibility.Visible;
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.Partial:
                    Icon = "/Images/link.png";
                    IconTooltip = "Some mods already downloaded";
                    IconColor = Yellow;

                    CheckBoxVisibility = Visibility.Hidden;

                    ButtonVisibility = Visibility.Visible;
                    ButtonText = "Subscribe";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.NeedsUpdate:
                    Icon = "/Images/download.png";
                    IconTooltip = "Needs update";
                    IconColor = Yellow;

                    CheckBoxVisibility = Visibility.Hidden;

                    ButtonVisibility = Visibility.Visible;
                    ButtonText = "Update";
                    ButtonIsEnabled = true;
                    break;

                case AddonGroupState.Ready:
                    Icon = "/Images/checkmark.png";
                    IconTooltip = "Ready";
                    IconColor = Green;

                    CheckBoxVisibility = Visibility.Visible;

                    ButtonVisibility = Visibility.Hidden;
                    ButtonText = "";
                    ButtonIsEnabled = false;
                    break;
            }
        }
    }
}
