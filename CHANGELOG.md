# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Common Changelog](https://common-changelog.org/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2024-07-24

- Increased stdout buffer size to 64 KB to handle larger save files
- De-prettied (uglied? uglified?! made ugly!?!) the JSON output to save buffer size
- Packaging with NativeAOT to improve performance

## [1.1.0] - 2024-07-15

- Added `JsonVersion` in case we have to change the output format in the future
- Added experimental write support. This isn't a save file editor but is a proof-of-concept testing the full circle of read, decompress, modify, compress, write. The game does successfully read the re-written save file.
- Code refactoring

## [1.0.1] - 2024-07-09

- Fixed batch file for whitespace in folder paths

## [1.0.0] - 2024-07-09

- Initial release

[1.2.0]: https://github.com/Nexus-Mods/StarfieldSaveTool/releases/tag/v1.2.0
[1.1.0]: https://github.com/Nexus-Mods/StarfieldSaveTool/releases/tag/v1.1.0
[1.0.1]: https://github.com/Nexus-Mods/StarfieldSaveTool/releases/tag/v1.0.1
[1.0.0]: https://github.com/Nexus-Mods/StarfieldSaveTool/releases/tag/v1.0.0