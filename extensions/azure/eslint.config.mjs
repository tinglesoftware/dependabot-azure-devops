import { config } from '@paklo/eslint/base';

/** @type {import("eslint").Linter.Config} */
export default [
  ...config,
  {
    ignores: ['tasks/*/dist/**'],
  },
];
