
export interface IDependabotUpdateOutput {
    success: boolean,
    error: Error,
    output: {
        type: string,
        data: any
    }
}
