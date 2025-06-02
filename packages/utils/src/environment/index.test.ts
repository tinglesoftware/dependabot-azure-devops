import { describe, expect, it } from 'vitest';
import { environment } from './index';

describe('environment', () => {
  it('works', () => {
    expect(environment.name).toBe('test');
    expect(environment.development).toBe(false);
    expect(environment.production).toBe(false);
    expect(environment.test).toBe(true);
    expect(environment.platform).toBeUndefined();
    expect(environment.sha).toBeUndefined();
    expect(environment.branch).toBeUndefined();
  });
});
