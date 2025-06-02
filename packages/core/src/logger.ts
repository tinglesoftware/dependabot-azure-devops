import { pino, type DestinationStream, type Logger, type LoggerOptions } from 'pino';
import { PinoPretty } from 'pino-pretty';
import { environment } from './environment';

const options: LoggerOptions = {
  level: process.env.LOG_LEVEL || (environment.production ? 'warn' : 'debug'),
  base: {
    env: environment.name,
    sha: environment.sha,
    branch: environment.branch,
  },
};

// pino-pretty has issues with nextjs and we cannot fix in webpack because we are moving to turbopack
// https://github.com/pinojs/pino/issues/1841#issuecomment-1815284760
// https://github.com/vercel/next.js/discussions/46987
const destination: DestinationStream | undefined = environment.production
  ? undefined
  : PinoPretty({
      colorize: true,
      // https://github.com/pinojs/pino-pretty#usage-with-jest
      sync: environment.test,
    });
const logger = pino(options, destination);

/** Options for creating a logger. */
type CreateOptions = {
  /**
   * The name of the application.
   * @example `website`
   */
  name: string;
};

/**
 * Creates a logger for the application.
 * @param {CreateOptions} options - The options for creating the logger.
 * @returns {Logger} The created logger.
 */
export function create({ name }: CreateOptions): Logger {
  const application = `paklo-${name}`;
  return logger.child({ application }, { level: environment.production ? 'warn' : 'debug' });
}
