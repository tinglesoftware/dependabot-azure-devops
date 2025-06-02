import { string, type RawCreateParams } from 'zod';

export type IsoDateParams = RawCreateParams & { coerce?: true };

/**
 * Schema for an ISO date string.
 * @param params - Options for the ISO date schema.
 * @returns A Zod object representing an ISO date string.
 */
export function isodate(params?: IsoDateParams) {
  return string(params)
    .refine((value) => !isNaN(Date.parse(value)), 'Invalid date string')
    .transform<string>((value) => new Date(value).toISOString());
}
