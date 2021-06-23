# kellerkompanie-sync-wpf
Native Windows implementation of KekoSync using the Windows Presentation Foundation (WPF) frontend in C#. The idea is to have an executable .exe that runs on Windows without requiring the user to install additional dependencies (e.g., Java JVM). KekoSync is used to synchronize addons and groups of addons from the Arma3 gameserver to force all players joining with the same set of addons. The overall intention is to provide our community members with a tool that is intuitive and simpler to use than the vanilla launcher, ArmA3Sync etc.

Server side implementation (written in Rust): https://github.com/kellerkompanie/kellerkompanie-sync-rust

## Features
* Synchronization of Arma3 addons and groups from server to client with multiple simultaneous and stoppable/resumable downloads.
* Detection of already present addons to avoid superflous downloads.
* Arma3 launcher with option selection (-nopause, etc.).
* Display of news from the kellerkompanie.com webpage.
* Shortcuts to the forum, server interface, teamspeak and TFAR installation.

## Building
1. Install Visual Studio 2019 Community edition (https://visualstudio.microsoft.com/downloads/) with the .NET desktop development package. Alternatively you can use Visual Studio Code with the appropriate plugins etc.
  ![grafik](https://user-images.githubusercontent.com/23381725/123174357-47c61b80-d480-11eb-954e-9615b5a7e9c8.png)


2. Clone the respository and open the .sln file within Visual Studio.
3. Run the code using the standard configuration. If asked permit the elevation of permissions (KekoSync needs it to write files in your addon directories).
