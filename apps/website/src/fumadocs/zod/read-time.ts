import readingTime, { type Options, type ReadTimeResults } from 'reading-time';
import { custom } from 'zod';

export type ReadingTimeParams = Options;

/**
 * Schema for a document's reading time.
 * @param params - Options for the reading time schema.
 * @returns A Zod object representing reading time data.
 */
export function readtime({ wordBound, wordsPerMinute, contents }: ReadingTimeParams & { contents: string }) {
  return custom().transform<ReadTimeResults>(() => readingTime(contents, { wordBound, wordsPerMinute }));
}
