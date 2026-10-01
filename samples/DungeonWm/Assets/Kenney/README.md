# Kenney sounds

DungeonWm plays its sound effects from two sound packs by Kenney
(www.kenney.nl). Both are CC0 1.0. Credit is not required, and DungeonWm
gives it here and in `docs/dungeon-wm.md`.

| Pack | Version | Source | Copied |
|---|---|---|---|
| Impact Sounds | 1.0 | https://kenney.nl/assets/impact-sounds | 2026-09-30 |
| RPG Audio | 1.0 | https://kenney.nl/assets/rpg-audio | 2026-09-30 |

Each file here is one OGG from a pack, converted to a 48 kHz mono 16-bit WAV
with `ffmpeg -ac 1 -ar 48000 -c:a pcm_s16le`. The sound is not changed
otherwise, and the name keeps the pack's own name.

| Files | Pack | Plays for |
|---|---|---|
| `footstep00` to `footstep09` | RPG Audio | a step, heel then toe, on the stone floor |
| `cloth1` to `cloth4` | RPG Audio | a jump |
| `impactSoft_heavy_000` to `004` | Impact Sounds | a landing |
| `impactWood_medium_000` to `004` | Impact Sounds | a light window impact |
| `impactWood_heavy_000` to `004` | Impact Sounds | a hard window impact |
| `metalLatch` | RPG Audio | a window that drops onto its hook |
| `metalClick` | RPG Audio | a plaque button |

`LICENSE-Impact.txt` and `LICENSE-RPG.txt` are the packs' own license files.

Nothing reads this directory at run time. The files are embedded resources.
