# Compatibility

What works in Know Your Role Recomp 0.2.0, and how it was tested. Everything was tested on Windows 11 x64 with the
NTSC-U disc (SLUS-01234).

- **By hand**: a person playing with a real controller.
- **Automated**: scripted runs that play the game through its normal input path (simulated SDL controllers and the
  keyboard) and check what is on screen, what is in the game's memory and what its logs say.

## At a glance

| Area | Status | Tested |
|---|---|---|
| Boot, title, menus | works | by hand and automated |
| Every part of the game (all 14 programs on the disc) | works, every one reached | automated |
| Exhibition matches, every match type | works | automated; a 1P match also by hand |
| Season | playing shows and saving work; loading a saved season with CONTINUE is **not confirmed** | automated |
| Create a Superstar, Taunt, Stable, Manager, P.P.V. | works, saved and loaded again | automated |
| Options | works | automated |
| Memory cards | works | automated |
| 1-4 players, automatic multitap | works | automated |
| DualSense over USB | works | **by hand** and automated |
| Other controllers, Bluetooth, keyboard | work with simulated controllers | automated |
| Graphics: OpenGL 3.3 or newer | works | automated |
| Graphics: a driver with only OpenGL 2.1 | the game starts and the menus draw; a match was not tested | automated |
| A 67-minute session of matches and menus | no errors, no memory growth | automated |

## Parts of the game

The game is one boot program plus 13 programs packed on the disc, swapped in while it runs. All are recompiled and
all were reached.

| Part | How to reach it |
|---|---|
| Boot, logos, movies | start |
| Title screen | boot |
| Menus, wrestler select | PRESS START |
| Match engine | any match |
| Season | MAIN MENU > SEASON |
| Shows and pay-per-view events (story scenes) | a Season show |
| Create a P.P.V. | MAIN MENU > CREATE A P.P.V. |
| Belt Records | MAIN MENU > BELT RECORDS |
| Rankings | MAIN MENU > RANKINGS |
| Create a Superstar (appearance) | MAIN MENU > CREATE A SUPERSTAR |
| Create a Superstar (move editor) | CREATE A SUPERSTAR > MOVES |
| Create a Taunt | MAIN MENU > CREATE A TAUNT |
| Create a Stable, Create a Manager | MAIN MENU > CREATE A STABLE / MANAGER |
| Options | MAIN MENU > OPTIONS |

## Matches

Each match type was started from the menus, played 30 seconds in the ring and left through the pause menu (EXIT
GAME). Cage, Hell in a Cell and Ladder were played 1P vs 2P: against the COM with an idle player 1, the COM may climb
out and win within seconds, which is the game's own rule.

| Category | Match types that reached the ring and played 30 seconds |
|---|---|
| Single | 1P vs COM (also to the finish), 1P vs 2P |
| Tag | Normal Tag, Tornado Tag; Normal Tag with 4 players (60 seconds, all four pressing buttons) |
| Anywhere Fall | Single, Tornado Tag, One on Two, One on Three, Triple Threat, Fatal 4 Way, Special Referee |
| Hardcore | Single, Tornado Tag, One on Two, One on Three, Triple Threat, Fatal 4 Way, Special Referee, Time Limit Title |
| Handicap | One on Two, One on Tag, One on Three |
| King of the Ring | Single Tournament, Special Tournament (first match, then EXIT from the bracket) |
| Royal Rumble | 1 player, 5 minutes: the entrant counter at 6, 12 and 19 after 1, 3 and 5 minutes |
| Survivor | Triple Threat, Fatal 4 Way, Battle Royal |
| Special | Casket, Cage, Hell in a Cell, I Quit, Iron Man, Ladder, Special Referee, Table, Slobber Knocker |

Played to their end, back to the menus afterwards: Single 1P vs COM, Anywhere Fall Single, Hardcore Triple Threat,
Survivor Fatal 4 Way, Normal Tag, Handicap One on Two, the Cage (1P vs COM: the COM climbed out) and a Season match.

By hand: a 1P vs COM match with a DualSense over USB, including pause and exit.

## Season

- Works: NEW GAME > the first show (story scenes, COM matches skipped) > the player's match fought to its end > the
  rest of the show > the next date. SAVE & EXIT writes the memory card.
- Not confirmed: loading the saved season with CONTINUE. It was seen working once, after a reset and after closing and
  restarting the game, but the latest automated runs did not confirm it: after closing and restarting the game the
  SEASON menu showed CONTINUE greyed out.

## Create modes, Rankings, Belt Records

| Mode | Tested |
|---|---|
| Create a Superstar | a new superstar through appearance, profile, personality, ability, moves and logic; saved to the memory card |
| Create a Superstar, move editor | a ground attack changed and saved (the game asks "save the data?" and then "overwrite saved data?", both default to No); still changed after closing and restarting the game |
| A created superstar in a match | last in the wrestler list; a 1P vs COM match played with him |
| Create a Taunt | a new taunt from a different base; saved; listed again after a restart |
| Create a Stable | a new stable with two members; saved; listed again after a restart |
| Create a Manager | a new superstar and manager pair; saved; listed again after a restart |
| Create a P.P.V. | renamed; DECISION starts the show (LIST / NEXT MATCH). The game keeps a created show only while it runs (leaving the editor says "Your created P.P.V. will be lost") |
| Rankings, Belt Records | all title pages; the created superstar is listed under OTHERS in the Rankings |

## Options

Options are chosen with Cross, which opens a list of values (left and right do nothing on this screen).

| Option | Result |
|---|---|
| DIFFICULTY | EASY / NORMAL / HARD are stored, used by the matches, kept on the memory card and loaded after a restart |
| SOUND | STEREO pans the sounds; MONO does not |
| BGM VOLUME | SILENT, NORMAL and MAX change the music's loudness |
| SE VOLUME | SILENT and MAX change the sound effects' loudness |
| VIBRATION | ON: the game's rumble reaches the controller; OFF: no rumble |
| PLAYER'S INDICATORS | ON: coloured rings under the wrestlers in a match |
| CAMERA ANGLES | the setting is kept; its effect on the match camera was not compared |

## Memory cards

The game keeps everything in one 3-block save on the card in slot 1: system data (options), created superstars,
taunts, stables and managers, and the Season.

| Tested | Result |
|---|---|
| SAVE SYSTEM DATA, then closing and restarting the game | saved; the next start loads it ("LOAD OK!") |
| Season, Create a Superstar, Taunt, Stable, Manager saves | each written to the card and found again after a restart |
| Card in slot 1 full (15 blocks used by other saves) | "INSUFFICIENT FREE BLOCKS!"; the card is left unchanged |
| No card in slot 1, card in slot 2 | "MEMORY CARD ISN'T INSERTED INTO MEMORY CARD SLOT 1. SAVE FAILED!"; the game never uses slot 2 |
| Updating over an earlier install | saves and settings are carried over unchanged and load at boot |

## Controllers

| Tested | How | Result |
|---|---|---|
| DualSense over USB | by hand | the launcher card shows it (DualSense, USB) and its buttons live; title, menus, a 1P match, pause and exit all work |
| Xbox 360 / One / Series, DualShock 4, DualSense, Switch Pro, Joy-Con pair, 8BitDo, generic pads | automated | each is recognized and drives the menus by button position |
| A pad without a layout | automated | a basic layout, then "Map this controller" in the launcher; the game uses the saved mapping |
| Bluetooth | automated | recognized; PlayStation pads rumble only when "Vibration over Bluetooth" is ticked |
| Vibration | automated | the game's rumble reaches the pad; VIBRATION OFF in the game or "Vibration" off in the launcher stops it |
| Unplugging and plugging back in | automated | in the menus; in a match (the game's CONTROLLER REMOVED pause, plugging back in resumes); pads added later become players 2-4 |
| Multitap | automated | on automatically with 3-4 pads, off again with 2 |
| 2 players without the multitap | automated | tag and single modes with 2 players selectable and playable |
| Keyboard | automated | player 1 alone through a whole match; player 2 on the second layout together with a pad |
| A pad shown twice by a remapper (DualSenseX, DS4Windows) | automated | the copy is ignored; two real pads pressed together stay two players |
| Analog stick | automated | the game reads the d-pad only; the left stick works through the d-pad |

## Graphics

| Graphics driver | Result |
|---|---|
| OpenGL 4.6 (NVIDIA GeForce GTX 1070), with the 3.3 and the 2.1 renderer | matches drawn correctly at 30 frames per second |
| A driver limited to OpenGL 3.3 | title, menus and the select screens drawn correctly; a match was not reached (the test driver was a slow software renderer) |
| A driver limited to OpenGL 2.1 | the game starts; title, menus and the Create a Superstar 3D preview drawn correctly; a match was not reached |
| No usable OpenGL at all | the message "The game could not start its graphics", a log entry and an error report; no crash |

## Performance

Frame rates on the test PC with VSync on: the game runs matches at its own 30 frames per second throughout (four
wrestlers, a 4-player tag match and the Royal Rumble included) and menus at 60; the window shows 60. No slowdown was
measured. The boot movie plays at its own 15 frames per second.

Memory: building the game from the disc peaks at about 0.85 GB; the game uses about 0.45-1.15 GB depending on the
scene (about 0.8 GB typically). The build took about 3 minutes on the test PC.

## Known issues

- Loading a saved Season with CONTINUE was not confirmed in the latest tests (saving works).
- A graphics driver with only OpenGL 2.1 starts the game and draws the menus; a match was not tested on one.
- Bluetooth controllers are supported but not yet tested on real hardware.
- The game has no analog stick support of its own; the left stick works as the d-pad.
