# Changelog

## 0.2.0 (2026-09-30) · first public release

### The game
- All 14 programs on the disc recompiled to native code and reached in testing: boot and movies, title and menus,
  every exhibition match type, Season with its shows and story scenes, Create a Superstar (including the move
  editor), Create a Taunt, Stable, Manager and P.P.V., Rankings, Belt Records and Options.
- 1-4 players; with 3 or 4 players a multitap is plugged in automatically.
- Memory card saves, kept as files (`carda.sav`, `cardb.sav`) in `KnowYourRole-files\user\`.
- No emulator core and no BIOS file. The release contains no game data: the game is built on your PC from your own
  disc.

### Setup and the launcher
- One program to start: **KnowYourRole.exe**. The first run asks for the disc image, checks it, checks this PC, builds
  the game with a progress bar and a time estimate, and starts it. Later runs open the launcher with PLAY, controller
  setup, display settings and the game files; "start the game right away next time" skips the launcher.
- Picking the .bin also works: the .cue beside it is used.
- Disc check: the game code files must match the NTSC-U release (SLUS-01234) exactly; the disc's data track is also
  compared with the known dump of the release (a different dump is only a note).
- If the disc image is moved, the launcher asks for it again.
- The folder holds only KnowYourRole.exe and README.txt; everything else is in `KnowYourRole-files\` (the game in
  `runtime\`, saves, settings and logs in `user\`, the licence notices).
- Updating: extract a new version over the old folder. Saves, settings and controller mappings stay; the launcher
  builds the game again from your disc once.
- Clear messages instead of a crash or an invisible game: no usable OpenGL, a folder Windows keeps read-only
  (Program Files), a missing Microsoft Visual C++ Redistributable (with an **Install it** button that opens
  Microsoft's download), too little memory or disk space for the build. A note, without stopping, when the folder is
  inside OneDrive.
- Graphics: OpenGL 4.5, 3.3 or 2.1, whichever the driver offers. VSync is on by default.
- The game writes a session log to `KnowYourRole-files\user\logs\`; "Collect diagnostics" packs it with the error
  reports and settings (user and PC names removed) for a bug report.
- The release zip is always named `KnowYourRole-win-x64.zip`, so the README links straight to the latest one.

### Controllers
- Controllers through SDL, with the community controller mapping database (SDL_GameControllerDB); a pad without any
  mapping still works with a basic layout. DualShock 4 and DualSense work directly, without DualSenseX or DS4Windows.
- A controller card per player in the launcher: the pad's family (Xbox, PlayStation, Nintendo, generic) and connection
  (USB, Bluetooth), a live button test, button labels for that family, "Ignore this controller" when a remapper shows
  a pad twice, and "Map this controller" for pads without a layout.
- The game's own vibration (OPTIONS > VIBRATION) reaches the controller. Over Bluetooth, PlayStation pads rumble only
  when "Vibration over Bluetooth" is ticked (it switches the pad into a mode that lasts until it is turned off).
- Unplugging a pad during a match brings up the game's own "controller removed" pause; plugging it back in resumes.
- Keyboard for player 1, and for player 2 on a second layout (off by default, one tick on player 2's card).
