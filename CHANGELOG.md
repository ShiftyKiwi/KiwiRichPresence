# Changelog

## 3.1.0.0

- Added `Zone Presets` for territory-specific Rich Presence customization
- Added searchable zone targeting with bulk-select helpers and current-zone selection
- Added zone-specific overrides for text, images, hover text, privacy, and party visibility
- Updated override precedence to include zone presets, with AFK remaining the final special-case layer
- Added an always-visible override-order note near the top of `/prp`

## 3.0.1.0

- Fork shipping release under the `KiwiRichPresence` identity
- Unique internal name for fork/pluginmaster distribution
- Renamed the project layout, solution, and build outputs around `KiwiRichPresence`
- Updated install metadata and support policy for the fork
- Added user-facing install and usage documentation
- Preserved the expanded customization feature set:
  - global overrides
  - context presets
  - job presets
  - custom large/small images
  - hover text
  - privacy controls
  - live preview and manual apply/testing
- Included the stabilization pass:
  - dirty/throttled refresh model
  - cached resolver lookups
  - config normalization/version 2 handling
  - safer Wine bridge ownership and live toggle behavior
