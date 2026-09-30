Know Your Role Recomp
=====================

An unofficial native Windows port of the 2000 PlayStation wrestling game with serial SLUS-01234.
This package contains no game data. It builds the game on your PC from your own copy of the disc.

Download
  Latest version: https://github.com/Comfubar/KnowYourRoleRecomp/releases/latest/download/KnowYourRole-win-x64.zip
  1. Extract the zip to a folder you can write to, for example C:\Games\KnowYourRole.
  2. Double-click KnowYourRole.exe.
  3. Pick your disc image's .cue file (the .bin next to it is found automatically). The disc is checked, the game
     is built (about three minutes) and then starts. Later, KnowYourRole.exe opens the launcher: press PLAY.

You need
  - Windows 10 or 11, 64-bit, with a graphics driver that supports OpenGL 3.3 or newer (a driver with only
    OpenGL 2.1 starts the game and draws its menus; a match was not tested on one)
  - 4 GB of memory or more (building peaks at about 0.85 GB, playing uses up to about 1.2 GB), and 1.5 GB of
    free disk space for setup (the built game takes about 250 MB)
  - your own NTSC-U disc image (serial SLUS-01234) as .cue + .bin
  - the Microsoft Visual C++ Redistributable (x64); most PCs have it. If it is missing, setup and PLAY say so
    and their "Install it" button opens Microsoft's download (https://aka.ms/vs/17/release/vc_redist.x64.exe)
No BIOS file is needed.

Where to put it
  Not in Program Files (Windows does not let programs write there) and not in a folder OneDrive syncs (OneDrive
  would upload the built game, about 250 MB including the code built from your disc). Keep the disc image where it
  is: the game reads it while it runs. If you move it, the launcher asks for it again.

Folders
  KnowYourRole.exe       the program: setup, launcher, controller settings, diagnostics
  README.txt             this file
  KnowYourRole-files\    everything else:
    runtime\             the game runtime and, after setup, the game built from your disc (never share it)
    user\                your saves (carda.sav, cardb.sav), settings, controller mappings and logs; back this up
    LICENSE.txt, THIRD-PARTY-NOTICES.txt

Updating
  Extract the new version over the old folder. Your saves and settings stay (from an older version they are
  carried over into KnowYourRole-files\user\); the launcher builds the game again once.

Controllers
  Up to 4 players; with 3 or 4 a multitap is plugged in automatically. The launcher's Controllers tab shows each
  player's pad, tests its buttons and maps pads the game does not know. If a remapper (DualSenseX, DS4Windows) makes
  one pad show up twice, the copy is ignored. The mouse works in the launcher and the in-game settings (F1) only.

If something goes wrong
  Launcher > Game files > Collect diagnostics (or KnowYourRole.exe --collect-diagnostics) packs the logs into one
  zip, with your user name and PC name removed, to attach to a bug report. Never attach game files or disc images.

Command line
  KnowYourRole.exe --play                    start the game directly
  KnowYourRole.exe --launcher                open the launcher even when "start the game right away" is on
  KnowYourRole.exe --cue <disc.cue>          build without a window
  KnowYourRole.exe --collect-diagnostics [--zip <file>]

This is an unofficial fan project, not affiliated with or endorsed by the game's publishers or developers or by
Sony Interactive Entertainment. All trademarks belong to their owners.
