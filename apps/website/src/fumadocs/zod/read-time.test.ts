import { describe, expect, it } from 'vitest';
import { readtime } from './read-time';

describe('readtime', () => {
  it('should calculate read time correctly', () => {
    const result = readtime({
      wordsPerMinute: 200, // default is 200
      contents: 'Lorem ipsum dolor sit amet, consectetur adipiscing elit.',
    }).parse(null);

    expect(result).toEqual({
      text: '1 min read',
      time: 2400,
      words: 8,
      minutes: 0.04,
    });
  });
});
