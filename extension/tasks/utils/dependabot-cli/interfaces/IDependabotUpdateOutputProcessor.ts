import { IDependabotUpdateJob } from "./IDependabotUpdateJob";

export interface IDependabotUpdateOutputProcessor {
    process(update: IDependabotUpdateJob, type: string, data: any): Promise<boolean>;
}
