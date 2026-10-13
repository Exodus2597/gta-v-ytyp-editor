# Release instructions

Repository: [Exodus2597/gta-v-ytyp-editor](https://github.com/Exodus2597/gta-v-ytyp-editor).

## Build and verify

```powershell
./scripts/build.ps1 -Test
```

The script downloads the pinned CodeWalker dependency, builds a self-contained executable, runs the self-tests, and prepares a Windows ZIP plus SHA-256 checksum under `artifacts/`.

The ZIP contains only `YtypEditor.exe`, `README.md`, and `licenses/`. Retain all licences/notices when distributing it. The editor's original source is MIT; dependencies retain their separate upstream terms.

## Publish a version

Update the project version and changelog, commit the changes, then create a release on the matching source commit. Using an authenticated GitHub CLI:

```powershell
gh release create v1.1.1 artifacts/gta-v-ytyp-editor-1.1.1-win-x64.zip artifacts/gta-v-ytyp-editor-1.1.1-win-x64.zip.sha256 --target main --title "YTYP Editor 1.1.1" --notes-file docs/CHANGELOG.md
```

GitHub CLI is optional; releases and assets can also be managed through GitHub's website. The Windows workflow builds/tests and uploads artifacts with read-only repository permissions; it does not publish releases automatically.

## Source hygiene

Commit source, scripts, workflow, docs, screenshots, and notices. Keep `vendor`, `artifacts`, `bin`, `obj`, and local test output out of commits and source ZIPs. Dependency source is downloaded during the build, rather than copied into the editor repository.

The automated checks are not an in-game compatibility test. Validate a real prop pack in GTA V before describing a release as game-tested.
