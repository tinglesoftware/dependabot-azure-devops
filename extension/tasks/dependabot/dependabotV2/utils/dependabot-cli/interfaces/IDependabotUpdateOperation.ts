import { IDependabotUpdate } from "../../dependabot/interfaces/IDependabotConfig"
import { IDependabotUpdateJobConfig } from "./IDependabotUpdateJobConfig"

/**
 * Represents a single Dependabot CLI update operation
 */
export interface IDependabotUpdateOperation extends IDependabotUpdateJobConfig {
    config: IDependabotUpdate
}
