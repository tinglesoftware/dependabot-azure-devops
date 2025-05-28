import { describe, expect, it } from 'vitest';
import { isodate } from './isodate';

describe('isodate', () => {
  it('should parse valid ISO date strings', () => {
    expect(isodate().parse('2022-01-01')).toBe('2022-01-01T00:00:00.000Z');
    expect(isodate().parse('2020-02-29')).toBe('2020-02-29T00:00:00.000Z'); // Leap year
    expect(isodate().parse('2022-12-31T23:59:59Z')).toBe('2022-12-31T23:59:59.000Z'); // With time
  });

  it('should throw error for invalid date strings', () => {
    expect(() => isodate().parse('invalid-date')).toThrowError('Invalid date string');
    expect(() => isodate().parse('2022-01-32')).toThrowError('Invalid date string'); // Invalid date
    expect(() => isodate().parse('2022-13-01')).toThrowError('Invalid date string'); // Invalid month
  });

  it('should parse valid ISO date strings with coerce option enabled', () => {
    expect(isodate({ coerce: true }).parse('2022-01-01')).toBe('2022-01-01T00:00:00.000Z');
    expect(isodate({ coerce: true }).parse('2020-02-29')).toBe('2020-02-29T00:00:00.000Z'); // Leap year
    expect(isodate({ coerce: true }).parse('2022-12-31T23:59:59Z')).toBe('2022-12-31T23:59:59.000Z'); // With time
  });

  it('should throw error for invalid date strings with coerce option enabled', () => {
    expect(() => isodate({ coerce: true }).parse('invalid-date')).toThrowError('Invalid date string');
    expect(() => isodate({ coerce: true }).parse('2022-01-32')).toThrowError('Invalid date string'); // Invalid date
    expect(() => isodate({ coerce: true }).parse('2022-13-01')).toThrowError('Invalid date string'); // Invalid month
  });
});
