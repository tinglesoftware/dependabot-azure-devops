import { config } from '@paklo/eslint/base';

/** @type {import("eslint").Linter.Config} */
export default [
  ...config,
  {
    ignores: ['tasks/*/dist/**'],
  },
  {
    rules: {
      // TODO: remove this after writing tests for src/dependabot/branch-name.ts
      'no-useless-escape': 'off',
    },
  },
];
