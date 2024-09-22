import { VersionControlChangeType } from "azure-devops-node-api/interfaces/TfvcInterfaces";

/**
 * File change
 */
export interface IFileChange {
    changeType: VersionControlChangeType,
    path: string,
    content: string,
    encoding: string
}
