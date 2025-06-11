import { readFile } from 'fs/promises';
import * as yaml from 'js-yaml';
import { describe, expect, it } from 'vitest';

import {
  DependabotConfigSchema,
  parseRegistries,
  parseUpdates,
  validateConfiguration,
  type DependabotRegistry,
  type DependabotUpdate,
} from './config';

describe('Parse configuration file', () => {
  it('Parsing works as expected', async () => {
    const config = await DependabotConfigSchema.parseAsync(
      yaml.load(await readFile('fixtures/config/dependabot.yml', 'utf-8')),
    );
    const updates = parseUpdates(config, '');
    expect(updates.length).toBe(5);

    // first
    const first = updates[0]!;
    expect(first.directory).toBe('/');
    expect(first.directories).toBeUndefined();
    expect(first['package-ecosystem']).toBe('docker');
    expect(first['insecure-external-code-execution']).toBeUndefined();
    expect(first.registries).toBeUndefined();

    // second
    const second = updates[1]!;
    expect(second.directory).toBe('/client');
    expect(second.directories).toBeUndefined();
    expect(second['package-ecosystem']).toBe('npm');
    expect(second['insecure-external-code-execution']).toBe('deny');
    expect(second.registries).toEqual(['reg1', 'reg2']);

    // third
    const third = updates[2]!;
    expect(third.directory).toBeUndefined();
    expect(third.directories).toEqual(['/src/client', '/src/server']);
    expect(third['package-ecosystem']).toBe('nuget');
    expect(JSON.stringify(third.groups)).toBe(
      '{"microsoft":{"patterns":["microsoft*"],"update-types":["minor","patch"]}}',
    );

    // fourth
    const fourth = updates[3]!;
    expect(fourth.directory).toBe('/');
    expect(fourth.directories).toBeUndefined();
    expect(fourth['package-ecosystem']).toBe('devcontainers');
    expect(fourth['open-pull-requests-limit']).toEqual(0);
    expect(fourth.registries).toBeUndefined();

    // fifth
    const fifth = updates[4]!;
    expect(fifth.directory).toBe('/');
    expect(fifth.directories).toBeUndefined();
    expect(fifth['package-ecosystem']).toBe('dotnet-sdk');
    expect(fifth['open-pull-requests-limit']).toEqual(5);
    expect(fifth.registries).toBeUndefined();
  });
});

describe('Parse registries', () => {
  it('Parsing works as expected', async () => {
    const config = await DependabotConfigSchema.parseAsync(
      yaml.load(await readFile('fixtures/config/sample-registries.yml', 'utf-8')),
    );
    const registries = parseRegistries(config, () => undefined);
    expect(Object.keys(registries).length).toBe(11);

    // composer-repository
    let registry = registries['composer']!; // eslint-disable-line dot-notation
    expect(registry.type).toBe('composer_repository');
    expect(registry.url).toBe('https://repo.packagist.com/example-company/');
    expect(registry['index-url']).toBeUndefined();
    expect(registry.registry).toBeUndefined();
    expect(registry.host).toBe('repo.packagist.com');
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBeUndefined();
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBe('octocat');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry['replaces-base']).toBeUndefined();

    // docker-registry
    registry = registries['dockerhub']!; // eslint-disable-line dot-notation
    expect(registry.type).toBe('docker_registry');
    expect(registry.url).toBeUndefined();
    expect(registry['index-url']).toBeUndefined();
    expect(registry.registry).toBe('registry.hub.docker.com');
    expect(registry.host).toBeUndefined();
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBeUndefined();
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBe('octocat');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry['replaces-base']).toBe(true);

    // git
    registry = registries['github-octocat']!;
    expect(registry.type).toBe('git');
    expect(registry.url).toBe('https://github.com');
    expect(registry['index-url']).toBeUndefined();
    expect(registry.registry).toBeUndefined();
    expect(registry.host).toBeUndefined();
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBeUndefined();
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBe('x-access-token');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry['replaces-base']).toBeUndefined();

    // hex-organization
    registry = registries['github-hex-org']!;
    expect(registry.type).toBe('hex_organization');
    expect(registry.url).toBeUndefined();
    expect(registry['index-url']).toBeUndefined();
    expect(registry.registry).toBeUndefined();
    expect(registry.host).toBeUndefined();
    expect(registry.key).toBe('key_1234567890');
    expect(registry.token).toBeUndefined();
    expect(registry.organization).toBe('github');
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBeUndefined();
    expect(registry.password).toBeUndefined();
    expect(registry['replaces-base']).toBeUndefined();

    // hex-repository
    registry = registries['github-hex-repository']!;
    expect(registry.type).toBe('hex_repository');
    expect(registry.url).toBe('https://private-repo.example.com');
    expect(registry.registry).toBeUndefined();
    expect(registry.host).toBeUndefined();
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBeUndefined();
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBe('private-repo');
    expect(registry['auth-key']).toBe('ak_1234567890');
    expect(registry['public-key-fingerprint']).toBe('pkf_1234567890');
    expect(registry.username).toBeUndefined();
    expect(registry.password).toBeUndefined();
    expect(registry['replaces-base']).toBeUndefined();

    // maven-repository
    registry = registries['maven-artifactory']!;
    expect(registry.type).toBe('maven_repository');
    expect(registry.url).toBe('https://artifactory.example.com');
    expect(registry['index-url']).toBeUndefined();
    expect(registry.registry).toBeUndefined();
    expect(registry.host).toBeUndefined();
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBeUndefined();
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBe('octocat');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry['replaces-base']).toBe(true);

    // npm-registry
    registry = registries['npm-github']!;
    expect(registry.type).toBe('npm_registry');
    expect(registry.url).toBeUndefined();
    expect(registry['index-url']).toBeUndefined();
    expect(registry.registry).toBe('npm.pkg.github.com');
    expect(registry.host).toBeUndefined();
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBe('tkn_1234567890');
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBeUndefined();
    expect(registry.password).toBeUndefined();
    expect(registry['replaces-base']).toBe(true);

    // nuget-feed
    registry = registries['nuget-azure-devops']!;
    expect(registry.type).toBe('nuget_feed');
    expect(registry.url).toBe('https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json');
    expect(registry['index-url']).toBeUndefined();
    expect(registry.registry).toBeUndefined();
    expect(registry.host).toBeUndefined();
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBeUndefined();
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBe('octocat@example.com');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry['replaces-base']).toBeUndefined();

    // python-index
    registry = registries['python-azure']!;
    expect(registry.type).toBe('python_index');
    expect(registry.url).toBeUndefined();
    expect(registry['index-url']).toBe('https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example');
    expect(registry.registry).toBeUndefined();
    expect(registry.host).toBeUndefined();
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBeUndefined();
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBe('octocat@example.com');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry['replaces-base']).toBe(true);

    // rubygems-server
    registry = registries['ruby-github']!;
    expect(registry.type).toBe('rubygems_server');
    expect(registry.url).toBe('https://rubygems.pkg.github.com/octocat/github_api');
    expect(registry['index-url']).toBeUndefined();
    expect(registry.registry).toBeUndefined();
    expect(registry.host).toBeUndefined();
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBe('tkn_1234567890');
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBeUndefined();
    expect(registry.password).toBeUndefined();
    expect(registry['replaces-base']).toBe(false);

    // terraform-registry
    registry = registries['terraform-example']!;
    expect(registry.type).toBe('terraform_registry');
    expect(registry.url).toBeUndefined();
    expect(registry['index-url']).toBeUndefined();
    expect(registry.registry).toBeUndefined();
    expect(registry.host).toBe('terraform.example.com');
    expect(registry.key).toBeUndefined();
    expect(registry.token).toBe('tkn_1234567890');
    expect(registry.organization).toBeUndefined();
    expect(registry.repo).toBeUndefined();
    expect(registry['auth-key']).toBeUndefined();
    expect(registry['public-key-fingerprint']).toBeUndefined();
    expect(registry.username).toBeUndefined();
    expect(registry.password).toBeUndefined();
    expect(registry['replaces-base']).toBeUndefined();
  });
});

describe('Validate registries', () => {
  it('Validation works as expected', () => {
    // const config = await DependabotConfigSchema.parseAsync(
    //   yaml.load(await readFile('fixtures/config/dependabot.yml', 'utf-8')),
    // );
    // let updates = parseUpdates(config);
    // expect(updates.length).toBe(2);

    const updates: DependabotUpdate[] = [
      {
        'package-ecosystem': 'npm',
        'directory': '/',
        'directories': undefined,
        'registries': ['dummy1', 'dummy2'],
      },
    ];

    const registries: Record<string, DependabotRegistry> = {
      dummy1: {
        type: 'nuget',
        url: 'https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json',
        token: 'pwd_1234567890',
      },
      dummy2: {
        'type': 'python-index',
        'url': 'https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example',
        'username': 'octocat@example.com',
        'password': 'pwd_1234567890',
        'replaces-base': true,
      },
    };

    // works as expected
    validateConfiguration(updates, registries);

    // fails: registry not referenced
    updates[0]!.registries = [];
    expect(() => validateConfiguration(updates, registries)).toThrow(
      `Registries: 'dummy1,dummy2' have not been referenced by any update`,
    );

    // fails: registry not configured
    updates[0]!.registries = ['dummy1', 'dummy2', 'dummy3'];
    expect(() => validateConfiguration(updates, registries)).toThrow(
      `Referenced registries: 'dummy3' have not been configured in the root of dependabot.yml`,
    );
  });
});
