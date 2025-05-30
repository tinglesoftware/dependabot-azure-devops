Thank you for contributing to the Dependabot for Azure DevOps project.

# Table of Contents

- [Contribution workflow](#contribution-workflow)
- [Development environment](#development-environment)
- [Submitting pull requests](#submitting-pull-requests)
  - [Unit tests](#unit-tests)
  - [Static code analyzers and linters](#static-code-analyzers-and-linters)
  - [Formatters](#formatters)
  - [Spelling](#spelling)
  - [Automatically run quality checks on commit](#automatically-run-quality-checks-on-commit)

# Contribution workflow

1. Fork the project.
1. Get the [development environment running](#development-environment).
1. Make your feature addition or bug fix.
1. Make sure all [required quality checks](#required-workflow-tasks) have passed.
1. Send a pull request.

# Development environment

Before contributing, you'll need to configure your local development environment; View the corresponding development guide for the component you'd like to contribute to:

- [Azure DevOps Extension](./docs/extensions/azure.md#development-guide)
- [Dependabot Server](./docs/server.md#development-guide)

# Submitting pull requests

If you plan on submitting a pull request, there are several quality checks that must pass; It is recommended that you run these before submitting the pull request. The checks are:

## Unit tests

All existing unit tests must pass.
View the corresponding unit test instructions for the component you'd like to test:

- [Azure DevOps Extension](./docs/extensions/azure.md#running-the-unit-tests)
- [Dependabot Server](./docs/server.md#running-the-unit-tests)

## Static code analyzers and linters

Some components use static code analyzers and linters.
View the corresponding instructions for each component:

- [Azure DevOps Extension](./docs/extensions/azure.md#running-prettier)
- [Dependabot Server](./docs/server.md#formatting)

## Formatters

```bash
pnpm install
pnpm format:check        # to check for formatting issues
pnpm format              # to automatically fix formatting issues
```

## Spelling

```bash
pip install codespell
codespell                   # to check for misspellings
codespell --write-changes   # to automatically fix misspellings
```

## Automatically run quality checks on commit

If you'd like spelling, formatting, and linting checks to be run automatically on commit, enable the [Husky](https://typicode.github.io/husky/how-to.html) git hooks using:

```bash
pnpm dlx husky
```

When enabled, your local commits will be rejected if any of the quality checks fail; Unit tests are **not** run due to their long execution time, you must still run them manually.
