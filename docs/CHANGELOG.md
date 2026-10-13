# Changes

## 1.1.1

- Replaced reflection-based field population with explicit typed assignments.
- Simplified texture/name event handlers and removed redundant layout aliases.
- Consolidated saving/building through one write path, preserving validation and backups.
- Separated prop data from document compilation and reused fixed field/axis lists.
- Added consistent C# formatting, pinned dependency setup, a build/package script, and a Windows CI workflow.
- Bundled runtime libraries in one executable. Release excludes test fixtures, previews, debug symbols, and unused content files; upstream notices remain included.
- Added an MIT licence for the editor's original source; dependency notices are retained separately.

## 1.1.0

- Fixed project header clipping.
- Combined prop tabs into one form with category headings.
- Fixed action-button clipping and retained buttons below the scrollable fields.

## 1.0.0

- Initial native YTYP/XML editor with prop management, YDR import, JSON exchange, undo/redo, backup saves, and verified native builds.
