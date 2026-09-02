# Cat Metro

A cat-themed puzzle game for Android: route cats on little trains to the right stations.

**The look we're building is `docs/LOOK.md`, with the concept art in `docs/reference/`.
That's the point of the project — open the images before changing anything visual.**

## Layout

- `unity/` — the game (Unity 6000.3.16f1)
- `dotnet/` — game logic as a C# library (Domain, Application, Content, Services, Validator)
  plus its tests. Solid and well covered.
- `content/levels/` — 17 authored levels
- `scripts/` — asset pipeline (generation, decimation, metrics) and the gates

```
bash scripts/check.sh      # lint + typecheck
bash scripts/test.sh       # test suite
bash scripts/build-apk.sh  # dev APK — run it yourself, Unity can't run sandboxed
```

## Gotchas that cost us real time

**Devices.** Run `adb devices -l` and read `model:` before any adb command. Serials in older
docs were wrong for a week.
`48121FDAP006X4` = Pixel 9 Pro, the target. `2G0YC5ZF7Z056Q` = Quest 3. `emulator-5554` = Pico
OS6 emulator. The last two belong to other projects — never install there.

**URP materials fail silently.** An FBX can carry its texture, pass every test, and still render
as a flat grey ghost because base colour never bound. Tests can't see this; a render can.
Same class of problem: `CatModelCatalog` rejects a bad prefab *silently* and leaves the
placeholder, so an empty-looking screen has no log line explaining it. `AdmittedEntryCount` is
the read-back.

**Generated art is inconsistent by nature.** Different scales, different forward axes, some with
display plinths. Correct it in the presentation layer — the model bytes are pinned by the
licensing record.

**`scripts/build.sh` was a no-op stub and has been removed.** `scripts/build-apk.sh` is the real path.
Unity needs the network on a cold Library and writes outside the sandbox, so builds run
unsandboxed — the human runs them.

**Unity `-runTests` must not get `-quit`** — it exits before tests run: exit 0, no results.

**Never `git commit -a`.** Unity builds drift 5 settings files and `dotnet restore` rewrites a
lock file.

**`rg` doesn't exist on CI** and is a shell function locally — use `grep -E` in scripts. Plain
`grep` is BRE, so it won't match a pattern using `|`, `(` or `+`.

**CI is weaker than it looks.** It checks out shallow (`fetch-depth` unset), so anything
depending on git history passes vacuously, and it never compiles C# at all.

**Generated art lives in `unity/Assets/Art/Generated/incoming/`** — gitignored, one machine only.
`curation-backups/` inside it holds the only provider-delivered copies of two paid assets.

## Two things that aren't negotiable

**Never read `.env`,** and never run a Google Play upload — that one is human-only.

**Licensing.** The 3D cats and props came from paid Meshy and Tripo accounts. Putting them in a
Play Store binary is a real commercial question the human decides deliberately. Local dev builds
on your own phone aren't distribution.

## How we work

Build the thing, show it, iterate. Small visible changes beat perfect abstractions. If something
needs a decision, ask in one sentence.

This repo used to run a heavy contract-and-review process. It's gone — don't reintroduce frozen
contracts, staged approval gates, or governance documents.
