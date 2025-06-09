import { describe, expect, it } from 'vitest';

import { extractPlaceholder } from './placeholder';

describe('Parse property placeholder', () => {
  it('Should return key with underscores', () => {
    const matches: RegExpExecArray[] = extractPlaceholder('PAT:${{MY_DEPENDABOT_ADO_PAT}}');
    expect(matches[0]![1]).toBe('MY_DEPENDABOT_ADO_PAT');
  });

  it('Should return the key', () => {
    const matches: RegExpExecArray[] = extractPlaceholder('PAT:${{PAT}}');
    expect(matches[0]![1]).toBe('PAT');
  });

  it('Without PAT: prefix should return key', () => {
    const matches: RegExpExecArray[] = extractPlaceholder('${{MY_DEPENDABOT_ADO_PAT}}');
    expect(matches[0]![1]).toBe('MY_DEPENDABOT_ADO_PAT');
  });

  it('Works when padded with spaces', () => {
    const matches: RegExpExecArray[] = extractPlaceholder('PAT:${{ MY_SECRET_VAR_NAME }}');
    expect(matches[0]![1]).toBe('MY_SECRET_VAR_NAME');
  });

  it('With malformed brackets should be null', () => {
    const matches: RegExpExecArray[] = extractPlaceholder('${MY_DEPENDABOT_ADO_PAT}');
    expect(matches[0]).toBeUndefined();
  });
});
