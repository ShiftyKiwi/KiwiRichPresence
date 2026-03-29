# KiwiRichPresence Usage Guide

## What the Plugin Does

`KiwiRichPresence` updates your Discord Rich Presence based on what you are doing in Final Fantasy XIV. Out of the box it can show things like your character, job, world, party state, and location, then layer customization on top of that.

Open the configuration window with:

```text
/prp
```

## Global Overrides

The `Global Overrides` section applies broad rules everywhere unless something more specific overrides them.

Use it when you want to:

- replace the top or second text line everywhere
- use a custom large image everywhere
- use a custom small image everywhere
- change the image hover text
- hide an image slot globally
- reduce location precision everywhere

## Context Presets

Context presets are situation-based overrides. They apply only when that situation is active.

Current contexts include:

- Menus
- Login Queue
- Open World
- Housing
- Duty
- AFK

Each preset can override:

- top line text
- second line text
- large image
- small image
- hover text
- privacy mode
- party visibility

## Job Presets

Job presets let you target one or more specific classes/jobs and apply a custom Rich Presence profile to them.

Typical uses:

- give healers a different look from tanks
- use crafter/gatherer-specific artwork
- give one favorite job its own custom icon and text

Job presets are checked in top-to-bottom order. The first enabled preset that matches your current job is used.

## Template Tokens

Text fields can use template tokens so your status updates dynamically instead of staying fully static.

Available tokens:

```text
{context} {details} {state} {name} {fc} {world} {home_world} {data_center}
{zone} {region} {location} {job} {job_short} {level} {status}
{queue_position} {queue_eta} {party_size} {party_max}
```

Example templates:

```text
{name} - {job_short} {level}
{location}
```

```text
{job} | {world}
```

## Custom Image and Icon Behavior

The plugin supports custom large and small images through direct public `http(s)` image URLs.

Notes:

- use direct image URLs, not page URLs
- local file paths are not supported
- square images usually fit best
- Discord controls the final rendering, so the plugin cannot force text effects, outlines, or custom text colors

The large image is typically the main artwork slot.

The small image is typically the job/status icon slot.

Both support custom hover text.

## Privacy and Location Behavior

Location display can be tuned at a high level:

- `Exact zone`
- `Region only`
- `Generic area`

That lets you choose between maximum detail and lighter privacy. Context presets and job presets can further override the global privacy mode when needed.

## Preview and Apply/Test Behavior

The plugin includes a live preview that shows the current resolved output for:

- active preset chain
- top line
- second line
- large image key or URL
- small image key or URL
- party visibility

Use `Apply/Test Now` to push the current working config immediately without closing the window. Use `Save and Close` to persist it.

## Override Precedence

At a high level, the plugin resolves presence in this order:

1. Automatic game-derived baseline
2. Global overrides
3. First matching enabled job preset
4. Matching enabled context preset

If a more specific layer changes the same field, it wins over the earlier layer.

## Fork Support

This is a heavily modified fork and should not be treated as the official Dalamud/pluginmaster release.

Use this repository for support:

- <https://github.com/ShiftyKiwi/Dalamud.RichPresence>
- <https://github.com/ShiftyKiwi/Dalamud.RichPresence/issues>
