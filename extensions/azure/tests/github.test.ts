import { readFile } from 'fs/promises';
import { describe, expect, it } from 'vitest';

import { SecurityVulnerabilitySchema } from '../src/github';

describe('SecurityVulnerabilitySchema', () => {
  it('works for sample', async () => {
    const fileContents = await readFile('../../advisories-example.json', 'utf-8');
    const privateVulnerabilities = await SecurityVulnerabilitySchema.array().parseAsync(JSON.parse(fileContents));
    expect(privateVulnerabilities).toBeDefined();
    expect(privateVulnerabilities.length).toBe(1);

    const value = privateVulnerabilities[0];
    expect(value.package).toStrictEqual({ name: 'Contoso.Utils' });
    expect(value.advisory).toBeDefined();
    expect(value.vulnerableVersionRange).toBe('< 3.0.1');
    expect(value.firstPatchedVersion).toStrictEqual({ identifier: '3.0.1' });
  });
});
