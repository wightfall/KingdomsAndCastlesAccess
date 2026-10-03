# Third-party software

KCAccess itself is MIT licensed (see `LICENSE`). Release packages also contain:

| Component | Version | License | Source |
|-----------|---------|---------|--------|
| Prism (`prism.dll`) – screen reader / TTS abstraction | 0.18.3 | Mozilla Public License 2.0 | https://github.com/ethindp/prism |
| BepInEx (in the `-with-BepInEx` package and the setup exe) | 5.4.23.5 | LGPL-2.1 | https://github.com/BepInEx/BepInEx |
| HarmonyX (part of BepInEx) | 2.x | MIT | https://github.com/BepInEx/HarmonyX |

Prism bundles code from other projects (NVDA controller client – LGPL-2.1, {fmt}, simdutf, highway,
dr_wav, concurrentqueue, moderncom, djinni, nvgt). Their licenses and Prism's `NOTICE` file are
shipped in `BepInEx/plugins/KCAccess/licenses/prism/`.

Kingdoms and Castles is © Lion Shield, LLC. This mod contains no game code or assets; it patches the
game at runtime.
