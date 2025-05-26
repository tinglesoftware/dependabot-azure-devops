import { execSync, type ExecException } from 'node:child_process';
import { existsSync } from 'node:fs';
import { describe, expect, it, vi } from 'vitest';
import { git } from './git.js';
import type { GitFileInfo } from './types.js';

vi.mock('node:child_process');
vi.mock('node:fs');

describe('git', () => {
  const defaultGitFileInfo: GitFileInfo = {
    date: new Date().toISOString(),
    timestamp: new Date().getTime(),
    author: 'unknown',
  };

  it('should throw an error if git is not installed', async () => {
    vi.mocked(execSync).mockImplementation(() => {
      throw new Error('git not found');
    });

    await expect(git({ path: 'test/file.txt' }).parseAsync({})).rejects.toThrow(
      'Failed to retrieve git history because git is not installed.',
    );
  });

  it('should throw an error if file does not exist', async () => {
    vi.mocked(execSync).mockReturnValue(Buffer.from('git version 2.30.0'));
    vi.mocked(existsSync).mockReturnValue(false);

    await expect(git({ path: 'test/file.txt' }).parseAsync({})).rejects.toThrow(
      'Failed to retrieve git history because the file does not exist.',
    );
  });

  it('should throw an error if git command fails', async () => {
    vi.mocked(execSync).mockImplementation((command) => {
      if (command.includes('--version')) {
        return Buffer.from('git version 2.30.0');
      }
      const error = new Error('git log failed') as ExecException;
      error.code = 1;
      error.stderr = 'fatal: bad revision';
      throw error;
    });
    vi.mocked(existsSync).mockReturnValue(true);

    await expect(git({ path: 'test/file.txt' }).parseAsync({})).rejects.toThrow(
      'Failed to retrieve the git history with exit code 1: fatal: bad revision',
    );
  });

  it('should return default value if file is not tracked by git', async () => {
    vi.mocked(execSync).mockReturnValue(Buffer.from('git version 2.30.0'));
    vi.mocked(existsSync).mockReturnValue(true);
    vi.mocked(execSync).mockReturnValue(Buffer.from(''));

    const result = await git({ path: 'test/file.txt', default: defaultGitFileInfo }).parseAsync({});
    expect(result).toEqual(defaultGitFileInfo);
  });

  it('should return git file info for newest commit', async () => {
    const timestamp = Math.floor(Date.now() / 1000);
    vi.mocked(execSync).mockReturnValue(Buffer.from(`${timestamp},John Doe`));
    vi.mocked(existsSync).mockReturnValue(true);

    const result = await git({ path: 'test/file.txt', author: true }).parseAsync({});
    expect(result).toEqual({
      date: new Date(timestamp * 1000).toISOString(),
      timestamp,
      author: 'John Doe',
    });
  });

  it('should return git file info for oldest commit', async () => {
    const timestamp = Math.floor(Date.now() / 1000);
    vi.mocked(execSync).mockReturnValue(Buffer.from(`${timestamp}`));
    vi.mocked(existsSync).mockReturnValue(true);

    const result = await git({ path: 'test/file.txt', age: 'oldest' }).parseAsync({});
    expect(result).toEqual({
      date: new Date(timestamp * 1000).toISOString(),
      timestamp,
      author: undefined,
    });
  });
});
