import { readFile } from 'fs/promises';
import * as yaml from 'js-yaml';
import { describe, expect, it } from 'vitest';

import { DependabotConfigSchema, parseRegistries } from './config';
import { makeCredentialsMetadata } from './job-builder';

describe('makeCredentialsMetadata', () => {
  it('works', async () => {
    const config = await DependabotConfigSchema.parseAsync(
      yaml.load(await readFile('fixtures/config/sample-registries.yml', 'utf-8')),
    );

    let registries = await parseRegistries(config, () => undefined);
    expect(Object.keys(registries).length).toBe(11);

    // TODO: change this to call makeCredentials/mapCredentials once it is moved to this package hence remove this hack
    registries = {
      main_source: {
        type: 'git_source',
        host: 'dev.azure.com',
        username: 'x-access-token',
        password: 'token',
      },
      github_source: {
        type: 'git_source',
        host: 'github.com',
        username: 'x-access-token',
        password: 'github-token',
      },
      ...registries,
    };
    const metadatas = makeCredentialsMetadata(Object.values(registries));
    expect(metadatas.length).toBe(13);

    expect(metadatas[0]).toEqual({ type: 'git_source', host: 'dev.azure.com' });
    expect(metadatas[1]).toEqual({ type: 'git_source', host: 'github.com' });
    expect(metadatas[2]).toEqual({
      type: 'composer_repository',
      host: 'repo.packagist.com',
      url: 'https://repo.packagist.com/example-company/',
    });
    expect(metadatas[3]).toEqual({
      'type': 'docker_registry',
      'replaces-base': true,
      'registry': 'registry.hub.docker.com',
    });
    expect(metadatas[4]).toEqual({ type: 'git', url: 'https://github.com' });
    expect(metadatas[5]).toEqual({ type: 'hex_organization', organization: 'github' });
    expect(metadatas[6]).toEqual({
      'type': 'hex_repository',
      'repo': 'private-repo',
      'public-key-fingerprint': 'pkf_1234567890',
      'url': 'https://private-repo.example.com',
    });
    expect(metadatas[7]).toEqual({
      'type': 'maven_repository',
      'replaces-base': true,
      'url': 'https://artifactory.example.com',
    });
    expect(metadatas[8]).toEqual({ 'type': 'npm_registry', 'replaces-base': true, 'registry': 'npm.pkg.github.com' });
    expect(metadatas[9]).toEqual({
      type: 'nuget_feed',
      url: 'https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json',
    });
    expect(metadatas[10]).toEqual({
      'type': 'python_index',
      'replaces-base': true,
      'index-url': 'https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example',
    });
    expect(metadatas[11]).toEqual({
      'type': 'rubygems_server',
      'replaces-base': false,
      'url': 'https://rubygems.pkg.github.com/octocat/github_api',
    });
    expect(metadatas[12]).toEqual({ type: 'terraform_registry', host: 'terraform.example.com' });
  });
});
