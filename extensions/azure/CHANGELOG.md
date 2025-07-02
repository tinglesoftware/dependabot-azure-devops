# extension-azure-devops

## 2.54.0

### Minor Changes

- 131d0f1: Added command to CLI to generate dependabot job files

### Patch Changes

- Updated dependencies [4f9929b]
- Updated dependencies [131d0f1]
- Updated dependencies [a257919]
  - paklo@0.4.0

## 2.53.2

### Patch Changes

- e1dc185: Fix filtering logic for existing pull requests between grouped and normal
- d010d50: Do not write YAML files with refs

## 2.53.1

### Patch Changes

- 5af507a: Added CLI with command to validate a dependabot configuration file
- Updated dependencies [5af507a]
- Updated dependencies [86822e2]
  - paklo@0.3.0

## 2.53.0

### Minor Changes

- 4e34a26: Fix dependabot cli execution environment variables
- aaf6698: Complete enforcing of strict typescript

### Patch Changes

- 761fb3e: Non-zero result from dependabot-cli should result in a failed result
- Updated dependencies [aaf6698]
  - paklo@0.2.0

## 2.52.1

### Patch Changes

- Updated dependencies [765bd89]
  - paklo@0.1.3

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
