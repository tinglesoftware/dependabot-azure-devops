# extension-azure-devops

## 2.52.0

### Minor Changes

- dbe39d1: Collect affected PRs for a given run and set output variable

### Patch Changes

- 47b79b3: Script typing improvements
- 22ee21d: Use ||= instead of ??= when finding go/dependabot tool
- Updated dependencies [47b79b3]
  - paklo@0.1.2

## 2.51.1

### Patch Changes

- 981fb6a: Replace ||= with ??= to preserve falsy values in default assignment
- Updated dependencies [981fb6a]
- Updated dependencies [57a09c2]
  - paklo@0.1.1

## 2.51.0

### Minor Changes

- d3ba65b: Treat assignees as optional reviewers
- e6c0ffa: Added a new core package which stores some logic to be shared by extensions, dashboard, and servers with validation of config via zod
- 8985a46: Add schemas for input and output hence validate scenarios

### Patch Changes

- cc3fb4c: Allow versioning of private packages without publishing
- eb5edee: Pass `enable-beta-ecosystems` to the job config
- 5301c73: Set `multi-ecosystem-update` in job config
- 0943939: Filter out empty entries from experiments input when parsing
- 335e4fe: Add changeset for easier change tracking and releasing
- Updated dependencies [1036cdf]
- Updated dependencies [eb5edee]
- Updated dependencies [5301c73]
- Updated dependencies [e6c0ffa]
- Updated dependencies [8985a46]
  - paklo@0.1.0
