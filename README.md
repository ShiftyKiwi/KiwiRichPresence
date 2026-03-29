# KiwiRichPresence

`KiwiRichPresence` is a forked and expanded release of the original FFXIV Dalamud Discord Rich Presence plugin. It keeps the original purpose intact, showing your Final Fantasy XIV activity in Discord, while adding deeper control over text, artwork, presets, privacy, and testing.

## Highlights

- Rich Presence for FFXIV with character, job, world, party, and location awareness
- Global overrides for status text, images, hover text, and privacy
- Context presets for menus, login queue, open world, housing, duties, and AFK
- Job presets for role- or class-specific presentation
- Custom large and small image URLs
- Template tokens for dynamic text
- Live preview plus `Apply/Test Now` support in `/prp`

## How to Install

1. Open XIVLauncher.
2. Go to `Settings` -> `Experimental`.
3. Add this custom plugin repository URL:

```text
https://raw.githubusercontent.com/ShiftyKiwi/KiwiRichPresence/main/pluginmaster.json
```

4. Save the setting and reopen the plugin installer.
5. Search for `KiwiRichPresence` and install it.
6. Open the configuration with `/prp`.

## Usage

The practical setup guide lives here:

- [Usage Guide](docs/USAGE.md)

It covers global overrides, context presets, job presets, template tokens, custom images/icons, privacy behavior, preview/apply behavior, and override precedence.

## Support

This is a heavily modified fork and should not be supported through the official Dalamud support flow or treated as the first-party plugin.

For support, bug reports, and fork-specific discussion, use this fork:

- Repository: <https://github.com/ShiftyKiwi/KiwiRichPresence>
- Issues: <https://github.com/ShiftyKiwi/KiwiRichPresence/issues>

## Credits

- Original plugin author: `goat`
- Original maintainer: `Franze`
- Fork edits and expanded customization: `ShiftyKiwi`

This fork preserves the original plugin's purpose while significantly expanding customization, preview tooling, preset logic, and release packaging.

## Release Notes

The current shipping release is `3.0.1.0`.

See [CHANGELOG.md](CHANGELOG.md) for the concise release summary.
