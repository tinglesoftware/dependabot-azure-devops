import pluginNext from '@next/eslint-plugin-next';
import { config as baseConfig } from '../../eslint-react.config.mjs';

/** @type {import("eslint").Linter.Config} */
export default [
  ...baseConfig,
  {
    plugins: {
      '@next/next': pluginNext,
    },
    rules: {
      ...pluginNext.configs.recommended.rules,
      ...pluginNext.configs['core-web-vitals'].rules,
    },
  },
];
