# Kenney sounds

LunaWm plays its notification and keyboard sounds from one sound pack by Kenney
(www.kenney.nl). It is CC0 1.0. Credit is not required, and LunaWm gives it
here and in `docs/luna-wm.md`.

| Pack | Version | Source | Copied |
|---|---|---|---|
| Interface Sounds | 1.0 | https://kenney.nl/assets/interface-sounds | 2026-10-03 |

Each file here is one OGG from the pack, converted to a 48 kHz mono 16-bit
WAV with `ffmpeg -ac 1 -ar 48000 -c:a pcm_s16le`. The sound is not changed
otherwise, and the name keeps the pack's own name.

| File | Plays for |
|---|---|
| `glass_002` | a banner |
| `question_001` | a popup alert |
| `click_001` | an on-screen keyboard key |
| `click_002` | the keyboard's Space |
| `click_003` | the keyboard's Backspace |
| `click_004` | the keyboard's Return |
| `tick_001` | a keyboard long-press list opening |

`LICENSE-Interface.txt` is the pack's own license file.

Nothing reads this directory at run time. The files are embedded resources.
