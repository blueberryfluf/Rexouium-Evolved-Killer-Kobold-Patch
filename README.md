# BlueBerry_fluf · Rexouium Evolved + Killer Kobold Carrier Patch
**by blueberry_fluf** · 0.1
Editor tool that patches [Killer Kobold](https://app.gumroad.com/killerkobold)’s carrier / vore system so it plays nicer on **Rexouium Evolved**.
Maw open, gulp, delayed belly + stomach size, burps, and lip sync — pick your avatar base and hit Patch.
---
## Requirements
- Unity + VRChat SDK  
- **Rexouium Evolved** avatar  
- **Killer Kobold** vore system already in the project (`Assets/VoreSystem`)
This repo / package is **only the patcher**. It does **not** include Killer Kobold or Rexouium Evolved.
---
## Install
1. Download `BlueBerry-RexouiumCarrierPatch-v1.0.0.unitypackage` from **Releases**
2. In Unity: **Assets → Import Package → Custom Package…**
3. Import everything
4. Open **Tools → BlueBerry → Rexouium Evolved Carrier Patch**
---
## Usage
1. Open the patch window  
2. Set **Target Avatar** (drag your Rexouium Evolved root, or **Use Hierarchy Selection**)  
3. Click **Patch Avatar**  
4. Re-enter Play Mode / rebuild with VRCFury, then test:
   **Show Carrier → Gulp → wait ~0.5s → belly / Stomach Size**
Re-run **Patch** after you update Killer Kobold assets.
You can also export a fresh `.unitypackage` from the tool: **Export .unitypackage…**
---
## What it patches (v1.0.0)
- Smooth maw open/close (`Jaw_Open` + funnel / cheeks / tongue)
- Gulp: jaw / tongue / neck — belly fills ~0.5s after gulp
- Stomach Size 0–6 / Wumbo → body belly (only after gulp)
- Burps + lip sync → `Jaw_Open` (on the avatar you select)
- Mute broken Stomach Churn (Churn shapes missing on Rexouium)
- Fix `BellySeat` path + rename **Move Seat → Gulp**
---
## Links
- GitHub: [github.com/blueberryfluf](https://github.com/blueberryfluf)
- Discord: [discord.gg/2s94q7hebm](https://discord.gg/2s94q7hebm)
- Instagram: [@blueberry_fluf](https://www.instagram.com/blueberry_fluf/)
Suggestions and bug reports welcome on Discord.
---
## License
Blueberry_fluf  
Free to use on your own avatars.  
Don’t reupload or resell this tool as your own.
Rexouium Evolved and Killer Kobold belong to their respective creators — buy / use those assets under their own terms.
