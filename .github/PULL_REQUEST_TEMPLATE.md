## What changed

Describe the change and the user or developer problem it addresses.

## Why

Explain why this change belongs in PaperRoute.

## Testing

For documentation-only changes, mark build/application checks as not applicable and describe the relevant documentation/link validation.

- [ ] `dotnet restore ManuscriptPipeline.slnx`
- [ ] `dotnet build ManuscriptPipeline.slnx --configuration Release`
- [ ] `dotnet test PaperRoute.Tests/PaperRoute.Tests.vbproj --configuration Release --no-build`
- [ ] Relevant manual workflow tested where applicable
- [ ] Light/Dark/System appearance checked for UI changes where applicable
- [ ] DPI/resizing behavior checked for UI changes where applicable

## Data and workflow safety

For persistence, migration, import/export, backup/restore, managed-file, or lifecycle changes, describe how existing user data and authoritative manuscript history remain protected.

## Notes

Call out schema, migration, installer/updater, accessibility, API, or compatibility considerations that deserve review.

## Tracking and documentation

- Linked issue(s):
- Roadmap/milestone impact (or not applicable):
- README, guide, release notes, or other documentation updated (or not applicable):

Record accepted findings and new development ideas in a linked issue or explicit roadmap proposal. A proposal does not become a release commitment until scoped.
