# KiwiRichPresence

`KiwiRichPresence` is a forked and expanded release of the original FFXIV Dalamud Discord Rich Presence plugin. It keeps the original goal intact, showing your Final Fantasy XIV activity in Discord, while adding deeper customization for images, text, presets, privacy, and preview/testing workflows.

## Highlights

- Custom Discord Rich Presence for FFXIV with zone, job, world, and party awareness
- Global overrides for text, images, hover text, and privacy
- Context presets for menus, login queue, open world, housing, duties, and AFK
- Job presets that let you swap presentation by class/job
- Custom large and small image URLs, plus custom hover text
- Template tokens for dynamic status text
- Live preview and `Apply/Test Now` support in `/prp`
- Privacy controls for exact zone, region-only, or generic location display

## How to Install

1. Open XIVLauncher.
2. Go to `Settings` -> `Experimental`.
3. In the custom plugin repositories field, add this URL:

```text
https://raw.githubusercontent.com/ShiftyKiwi/Dalamud.RichPresence/master/pluginmaster.json
```

4. Save the setting and reopen the plugin installer.
5. Search for `KiwiRichPresence` and install it.
6. Open the configuration with `/prp`.

### About the Requested `KiwiRichPresence` Raw URL

The requested URL:

```text
https://raw.githubusercontent.com/ShiftyKiwi/KiwiRichPresence/main/pluginmaster.json
```

is not the correct install URL for the current repository state, because this fork currently lives at `ShiftyKiwi/Dalamud.RichPresence` on the `master` branch. That requested URL would become valid if the GitHub repository were renamed to `KiwiRichPresence` and the default branch were changed to `main`.

## User Guide

A practical setup and customization guide is available here:

- [Usage Guide](docs/USAGE.md)

That guide covers global overrides, context presets, job presets, template tokens, custom images/icons, privacy behavior, preview/apply behavior, and override precedence.

## Attribution and Support

### Credits

- Original plugin author: `goat`
- Original maintainer: `Franze`
- Fork/customization work: `ShiftyKiwi`

This fork is based on the original plugin concept and codebase, with substantial changes for customization, preview tooling, preset logic, packaging, and release behavior.

### Support Policy

This fork should **not** be supported through the official Dalamud Discord/server or treated as the first-party/whitelisted plugin. It is a heavily modified fork with custom behavior and release packaging.

For support, bug reports, and fork-specific discussion, please use this fork instead:

- Repository: <https://github.com/ShiftyKiwi/Dalamud.RichPresence>
- Issues: <https://github.com/ShiftyKiwi/Dalamud.RichPresence/issues>

## Release Notes

The current forked/customized shipping release is `3.0.0.0`.

See [CHANGELOG.md](CHANGELOG.md) for a concise release summary.
