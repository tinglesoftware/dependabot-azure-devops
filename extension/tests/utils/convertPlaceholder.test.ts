import { extractPlaceholder } from '../../task/utils/convertPlaceholder';

describe('Parse property placeholder', () => {
  it('Should return key with underscores', () => {
    var matches: RegExpExecArray[] = extractPlaceholder('PAT:${{MY_DEPENDABOT_ADO_PAT}}');
    expect(matches[0][1]).toBe('MY_DEPENDABOT_ADO_PAT');
  });

  it('Should return the key', () => {
    var matches: RegExpExecArray[] = extractPlaceholder('PAT:${{PAT}}');
    expect(matches[0][1]).toBe('PAT');
  });

  it('Without PAT: prefix should return key', () => {
    var matches: RegExpExecArray[] = extractPlaceholder('${{MY_DEPENDABOT_ADO_PAT}}');
    expect(matches[0][1]).toBe('MY_DEPENDABOT_ADO_PAT');
  });

  it('With malformed brackets should be null', () => {
    var matches: RegExpExecArray[] = extractPlaceholder('${MY_DEPENDABOT_ADO_PAT}');
    expect(matches[0]).toBe(undefined);
  });
});
