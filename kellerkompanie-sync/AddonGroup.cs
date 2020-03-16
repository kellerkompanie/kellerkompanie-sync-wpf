using Serilog;
using System.Windows;
using System.Windows.Controls;

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

    public class AddonGroup
    {
        private const string Green = "#5cb85c";
        private const string Red = "#d9534f";
        private const string Yellow = "#f7c516";


        public WebAddonGroupBase WebAddonGroupBase { get; set; }

        private string icon;
        public string Icon
        {
            get { return icon; }
            set { icon = value; Parent?.Items.Refresh(); }
        }

        public string iconTooltip;
        public string IconTooltip
        {
            get { return iconTooltip; }
            set { iconTooltip = value; Parent?.Items.Refresh(); }
        }

        private string iconColor;
        public string IconColor
        {
            get { return iconColor; }
            set { iconColor = value; Parent?.Items.Refresh(); }
        }

        private string buttonText;
        public string ButtonText
        {
            get { return buttonText; }
            set { buttonText = value; Parent?.Items.Refresh(); }
        }

        private bool buttonIsEnabled;
        public bool ButtonIsEnabled
        {
            get { return buttonIsEnabled; }
            set { buttonIsEnabled = value; Parent?.Items.Refresh(); }
        }

        private Visibility buttonVisibility;
        public Visibility ButtonVisibility
        {
            get { return buttonVisibility; }
            set { buttonVisibility = value; Parent?.Items.Refresh(); }
        }

        private Visibility checkBoxVisibility;
        public Visibility CheckBoxVisibility
        {
            get { return checkBoxVisibility; }
            set { checkBoxVisibility = value; Parent?.Items.Refresh(); }
        }

        private bool checkBoxIsSelected;
        public bool CheckBoxIsSelected
        {
            get { return checkBoxIsSelected; }
            set { checkBoxIsSelected = value; Parent?.Items.Refresh(); }
        }

        private string statusText;
        public string StatusText
        {
            get { return statusText; }
            set { statusText = value; Parent?.Items.Refresh(); }
        }

        private Visibility statusVisibility;
        public Visibility StatusVisibility
        {
            get { return statusVisibility; }
            set { statusVisibility = value; Parent?.Items.Refresh(); }
        }

        public ListView Parent { get; set; }

        private AddonGroupState state;
        public AddonGroupState State
        {
            get
            {
                return state;
            }
            set
            {
                Log.Information("setting " + WebAddonGroupBase.Name + " to " + value);
                state = value;
                switch (state)
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

                //Application.Current.Dispatcher.Invoke(new Action(() =>
                //{
                Parent?.Items.Refresh();
                //}));
            }
        }

        public AddonGroup(WebAddonGroupBase WebAddonGroupBase)
        {
            this.WebAddonGroupBase = WebAddonGroupBase;
            State = AddonGroupState.Unknown;
        }
    }
}
