/**
 * Pull request properties
 */
export interface IPullRequestProperties {
  id: number;
  properties?: {
    name: string;
    value: string;
  }[];
}
