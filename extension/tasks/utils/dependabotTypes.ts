
export interface IDependabotUpdateJob {
    job: {
        // See: https://github.com/dependabot/dependabot-core/blob/main/updater/lib/dependabot/job.rb
        'id': string,
        'package-manager': string,
        'updating-a-pull-request': boolean,
        'dependency-group-to-refresh'?: string,
        'dependency-groups'?: {
            'name': string,
            'applies-to'?: string,
            'update-types'?: string[],
            'rules': {
                'patterns'?: string[]
                'exclude-patterns'?: string[],
                'dependency-type'?: string
            }[]
        }[],
        'dependencies'?: string[],
        'allowed-updates'?: {
            'dependency-name'?: string,
            'dependency-type'?: string,
            'update-type'?: string
        }[],
        'ignore-conditions'?: {
            'dependency-name'?: string,
            'version-requirement'?: string,
            'source'?: string,
            'update-types'?: string[]
        }[],
        'security-updates-only': boolean,
        'security-advisories'?: {
            'dependency-name': string,
            'affected-versions': string[],
            'patched-versions': string[],
            'unaffected-versions': string[],
            'title'?: string,
            'description'?: string,
            'source-name'?: string,
            'source-url'?: string
        }[],
        'source': {
            'provider': string,
            'api-endpoint'?: string,
            'hostname': string,
            'repo': string,
            'branch'?: string,
            'commit'?: string,
            'directory'?: string,
            'directories'?: string[]
        },
        'existing-pull-requests'?: {
            'dependencies': {
                'name': string,
                'version'?: string,
                'removed': boolean,
                'directory'?: string
            }[]
        },
        'existing-group-pull-requests'?: {
            'dependency-group-name': string,
            'dependencies': {
                'name': string,
                'version'?: string,
                'removed': boolean,
                'directory'?: string
            }[]
        },
        'commit-message-options'?: {
            'prefix'?: string,
            'prefix-development'?: string,
            'include'?: string,
        },
        'experiments'?: any,
        'max-updater-run-time'?: number,
        'reject-external-code'?: boolean,
        'repo-contents-path'?: string,
        'requirements-update-strategy'?: string,
        'lockfile-only'?: boolean
    },
    credentials: {
        // See: https://github.com/dependabot/dependabot-core/blob/main/common/lib/dependabot/credential.rb
        'type': string,
        'host'?: string,
        'url'?: string,
        'registry'?: string,
        'region'?: string,
        'username'?: string,
        'password'?: string,
        'token'?: string,
        'replaces-base'?: boolean
    }[]
}

export interface IDependabotUpdateOutput {
    success: boolean,
    error: any,
    output: {
        // See: https://github.com/dependabot/smoke-tests/tree/main/tests
        type: string,
        data: any
    }
}

export interface IDependabotUpdateOutputProcessor {
    process(type: string, data: any): Promise<boolean>;
}
