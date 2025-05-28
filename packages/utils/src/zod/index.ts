import { z } from 'zod';
import { git } from './git';
import { image } from './image';
import { type IsoDateParams, isodate } from './isodate';
import { readtime } from './read-time';

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

export type * from './types';
export { mod as z };
