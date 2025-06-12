import { type Command, type ErrorOptions } from 'commander';
import { type ZodType } from 'zod/v4';
import { logger } from '../logger';

export type HandlerErrorOptions = ErrorOptions & {
  /** The error message. */
  message: string;
};

export type HandlerOptions<T> = {
  /** The parsed options. */
  options: T;

  /** The command instance. */
  command: Command;

  /**
   * Log an error message and exit the process with the given exit code.
   * @param options - The error message or error options.
   */
  error: (options: string | HandlerErrorOptions) => void;
};

/* eslint-disable @typescript-eslint/no-explicit-any */
export type CreateHandlerOptions<T> = {
  schema: ZodType<T>;
  input: Record<string, any>;
  command: any;
};
/* eslint-enable @typescript-eslint/no-explicit-any */

export async function handlerOptions<T>({
  schema,
  input,
  command: cmd,
}: CreateHandlerOptions<T>): Promise<HandlerOptions<T>> {
  const options = await schema.parseAsync(input);
  const command = cmd as Command;
  return {
    options,
    command,
    error: (options) => {
      const { message, code, exitCode } = typeof options === 'string' ? { message: options } : options;
      logger.error(message);
      command.error('', { code, exitCode });
    },
  };
}
