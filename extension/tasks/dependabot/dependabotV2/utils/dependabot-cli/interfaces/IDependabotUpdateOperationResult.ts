
/**
 * Represents the output of a Dependabot CLI update operation
 */
export interface IDependabotUpdateOperationResult {
    success: boolean,
    error: Error,
    output: {
        type: string,
        data: any
    }
}
