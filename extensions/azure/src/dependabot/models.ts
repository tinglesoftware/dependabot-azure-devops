import { type DependabotInput, type DependabotOutput, type DependabotUpdate } from 'paklo/dependabot';

/**
 * Represents a single Dependabot CLI update operation
 */
export type IDependabotUpdateOperation = DependabotInput & {
  config: DependabotUpdate;
};

/**
 * Represents the output of a Dependabot CLI update operation
 */
export type IDependabotUpdateOperationResult = {
  success: boolean;
  error?: Error;
  output?: DependabotOutput;
  pr?: number;
};
