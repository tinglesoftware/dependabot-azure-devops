# paklo

## 0.3.0

### Minor Changes

- 5af507a: Added CLI with command to validate a dependabot configuration file

### Patch Changes

- 86822e2: Fix invalid yaml references

## 0.2.0

### Minor Changes

- aaf6698: Complete enforcing of strict typescript

## 0.1.3

### Patch Changes

- 765bd89: pr-title and comimt-message are often omitted in update_pull_request

## 0.1.2

### Patch Changes

- 47b79b3: Script typing improvements

## 0.1.1

### Patch Changes

- 981fb6a: Replace ||= with ??= to preserve falsy values in default assignment
- 57a09c2: Coerce parsing of updated-at in ignore-conditions

## 0.1.0

### Minor Changes

- e6c0ffa: Added a new core package which stores some logic to be shared by extensions, dashboard, and servers with validation of config via zod
- 8985a46: Add schemas for input and output hence validate scenarios

### Patch Changes

- 1036cdf: Update default experiments as of 09 June 2025
- eb5edee: Pass `enable-beta-ecosystems` to the job config
- 5301c73: Set `multi-ecosystem-update` in job config
