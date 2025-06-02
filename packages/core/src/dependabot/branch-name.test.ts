import { describe, expect, it } from 'vitest';
import { getBranchNameForUpdate, sanitizeRef } from './branch-name';

describe('getBranchNameForUpdate', () => {
  it('generates correct branch name for a single dependency update', () => {
    const result = getBranchNameForUpdate('npm', 'main', '/packages/ui', undefined, [
      { 'dependency-name': 'lodash', 'dependency-version': '4.17.21' },
    ]);
    expect(result).toBe('dependabot/npm/main/packages/ui/lodash-4.17.21');
  });

  it('generates correct branch name for a removed dependency', () => {
    const result = getBranchNameForUpdate('npm', 'main', '/', undefined, [
      { 'dependency-name': 'react', 'removed': true },
    ]);
    expect(result).toBe('dependabot/npm/main/react-removed');
  });

  it('generates correct branch name for a grouped update (with group name)', () => {
    const result = getBranchNameForUpdate('nuget', 'develop', '/', 'microsoft', [
      { 'dependency-name': 'Microsoft.Extensions.Logging', 'dependency-version': '1.0.0' },
      { 'dependency-name': 'Microsoft.Extensions.Http', 'dependency-version': '2.0.0' },
    ]);
    expect(result).toMatch(/^dependabot\/nuget\/develop\/microsoft-[a-f0-9]{10}$/);
  });

  it('generates correct branch name for multiple dependencies (no group name)', () => {
    const result = getBranchNameForUpdate('pip', 'main', '/src', undefined, [
      { 'dependency-name': 'numpy', 'dependency-version': '1.24.0' },
      { 'dependency-name': 'pandas', 'dependency-version': '2.1.0' },
    ]);
    expect(result).toMatch(/^dependabot\/pip\/main\/src\/multi-[a-f0-9]{10}$/);
  });

  it('generates correct branch name when version has square brackets', () => {
    const result = getBranchNameForUpdate('npm', 'main', '/', undefined, [
      { 'dependency-name': 'something', 'dependency-version': '[14435324]-1.0' },
    ]);

    expect(result).toBe(`dependabot/npm/main/something-14435324-1.0`);
  });

  it('generates correct branch name without branch', () => {
    const result = getBranchNameForUpdate('pip', undefined, '/', undefined, [
      { 'dependency-name': 'numpy', 'dependency-version': '1.24.0' },
    ]);

    expect(result).toBe(`dependabot/pip/numpy-1.24.0`);
  });

  it('respects custom separator', () => {
    const result = getBranchNameForUpdate(
      'npm',
      'main',
      '/',
      undefined,
      [{ 'dependency-name': 'lodash', 'dependency-version': '4.17.21' }],
      '__',
    );
    expect(result).toBe('dependabot__npm__main__lodash-4.17.21');
  });

  it('uses default separator when undefined', () => {
    const result = getBranchNameForUpdate(
      'npm',
      'main',
      '/',
      undefined,
      [{ 'dependency-name': 'lodash', 'dependency-version': '4.17.21' }],
      undefined,
    );
    expect(result).toBe('dependabot/npm/main/lodash-4.17.21');
  });

  it('normalizes directory with leading/trailing slashes', () => {
    const result = getBranchNameForUpdate(
      'npm',
      'main',
      '/some/deep/path/',
      undefined,
      [{ 'dependency-name': 'express', 'dependency-version': '4.18.2' }],
      '-',
    );

    expect(result).toBe(`dependabot-npm-main-some-deep-path-express-4.18.2`);
  });
});

describe('sanitizeRef', () => {
  it('removes forbidden characters', () => expect(sanitizeRef(['feat', 'abc$', '%de*f'], '/')).toBe('feat/abc/def'));
  it('replaces dots following slashes with dot-', () => expect(sanitizeRef(['fix', '.way'], '/')).toBe('fix/dot-way'));
  it('squeezes multiple slashes', () => expect(sanitizeRef(['a//b', 'c', 'd'], '/')).toBe('a/b/c/d'));
  it('squeezes multiple periods', () => expect(sanitizeRef(['a..b', 'c..d'], '/')).toBe('a.b/c.d'));
  it('squeezes multiple slashes and periods', () => expect(sanitizeRef(['a//b..c', '', 'd'], '/')).toBe('a/b.c/d'));
  it('removes trailing period', () => expect(sanitizeRef(['release', 'v1.0.0.'], '/')).toBe('release/v1.0.0'));
  it('handles all steps', () => expect(sanitizeRef(['a//b..c', '', '.d', '1.1.'], '/')).toBe('a/b.c/dot-d/1.1'));
});
