import { IDependabotUpdateOperation } from './IDependabotUpdateOperation';

/**
 * Represents a processor for Dependabot update operation outputs
 */
export interface IDependabotUpdateOutputProcessor {
  /**
   * Process the output of a Dependabot update operation
   * @param update The update operation
   * @param type The output type (e.g. "create-pull-request", "update-pull-request", etc.)
   * @param data The output data object related to the type
   */
  process(update: IDependabotUpdateOperation, type: string, data: any): Promise<boolean>;
}
