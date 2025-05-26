import { z } from 'zod';
import { git } from './git.js';
import { image } from './image.js';
import { type IsoDateParams, isodate } from './isodate.js';
import { readtime } from './read-time.js';

const mod = {
  ...z,
  isodate,
  readtime,
  image,
  git,

  coerce: {
    ...z.coerce,
    isodate: (params?: IsoDateParams) => isodate({ coerce: true, ...params }),
  },
};

export type * from './types.js';
export { mod as z };
