import { execSync, type ExecException } from 'node:child_process';
import { existsSync } from 'node:fs';
import { basename, dirname } from 'node:path';
import { custom } from 'zod';
import type { GitFileInfo } from './types.js';

// inspired by Docusaurus at:
// https://github.com/facebook/docusaurus/blob/4aef958a99bcd7e38886db0c3ba0517f5c1827e7/packages/docusaurus-utils/src/gitUtils.ts#L27
// https://github.com/facebook/docusaurus/blob/4aef958a99bcd7e38886db0c3ba0517f5c1827e7/packages/docusaurus-plugin-content-docs/src/lastUpdate.ts

export type GitParams = {
  /**
   * Which commit to use for the file's git information.
   *
   * @enum `'oldest'` - The commit that added the file, following renames.
   * @enum `'newest'` - The last commit that edited the file.
   *
   * @default 'newest'
   */
  age?: 'oldest' | 'newest';

  /**
   * Whether to pull author from git commit history.
   *
   * @default false
   */
  author?: boolean;

  /**
   * Default value to use if git information cannot be retrieved such as
   * the the file is not yet tracked by git (new files).
   *
   * @default `{ date: new Date().toISOString(), timestamp: new Date().getTime(), author: 'unknown' }`
   */
  default?: GitFileInfo;
};

/**
 * Schema for a file's git file info.
 *
 * @description
 * It gets the commit date instead of author date so that amended commits
 * can have their dates updated.
 *
 * @param param - Options for the git file info schema.
 * @returns A Zod object representing git file info.
 */
export function git({
  age = 'newest',
  author = false,
  default: defaultValue = { date: new Date().toISOString(), timestamp: new Date().getTime(), author: 'unknown' },
  path,
}: GitParams & { path: string }) {
  return custom().transform<GitFileInfo>(async (value, { addIssue }): Promise<GitFileInfo> => {
    try {
      // check if git is installed
      try {
        execSync('git --version', { stdio: 'ignore' });
      } catch {
        throw new Error('Failed to retrieve git history because git is not installed.');
      }

      // check if the file exists
      if (!existsSync(path)) throw new Error('Failed to retrieve git history because the file does not exist.');

      const args = [`--format=%ct,%an`, '--max-count=1', age === 'oldest' && '--follow --diff-filter=A']
        .filter(Boolean)
        .join(' ');

      let result: Buffer;
      try {
        result = execSync(`git log ${args} -- "${basename(path)}"`, {
          // Setting cwd is important, see: https://github.com/facebook/docusaurus/pull/5048
          cwd: dirname(path),
          stdio: 'pipe', // To capture stdout and stderr
        });
      } catch (error) {
        const err = error as ExecException;
        throw new Error(`Failed to retrieve the git history with exit code ${err.code}: ${err.stderr}`);
      }

      const output = result.toString().trim();
      if (!output) {
        return defaultValue;
        // throw new Error('Failed to retrieve the git history because the file is not tracked by git.');
      }

      const regex = author ? /^(?<timestamp>\d+),(?<author>.+)$/ : /^(?<timestamp>\d+)$/;
      const match = output.match(regex);
      if (!match) {
        return defaultValue;
        // throw new Error(`Failed to retrieve the git history with unexpected output: ${output}`);
      }

      const timestamp = Number(match.groups!.timestamp);
      const date = new Date(timestamp * 1000).toISOString();
      return { date, timestamp, author: match.groups!.author! };
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      addIssue({ fatal: true, code: 'custom', message });
      return null as never;
    }
  });
}
