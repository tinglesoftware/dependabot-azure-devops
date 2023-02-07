import { load } from "js-yaml";
import * as fs from "fs";
import * as path from "path";
import { parseRegistries } from "../../task/utils/parseConfigFile";

describe("Parse registries", () => {
  it("Parsing works as expected", () => {
    let config: any = load(fs.readFileSync('tests/utils/sample-registries.yml', "utf-8"));
    let registries = parseRegistries(config);
    expect(registries.length).toBe(11);

    // composer-repository
    var registry = registries[0];
    expect(registry.type).toBe('composer_repository');
    expect(registry.url).toBe('https://repo.packagist.com/example-company/');
    expect(registry.registry).toBe(undefined);
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe(undefined);
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe('octocat');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry["replaces-base"]).toBe(undefined);

    // docker-registry
    registry = registries[1];
    expect(registry.type).toBe('docker_registry');
    expect(registry.url).toBe(undefined);
    expect(registry.registry).toBe('registry.hub.docker.com');
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe(undefined);
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe('octocat');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry["replaces-base"]).toBe(true);

    // git
    registry = registries[2];
    expect(registry.type).toBe('git');
    expect(registry.url).toBe('https://github.com');
    expect(registry.registry).toBe(undefined);
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe(undefined);
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe('x-access-token');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry["replaces-base"]).toBe(undefined);

    // hex-organization
    registry = registries[3];
    expect(registry.type).toBe('hex_organization');
    expect(registry.url).toBe(undefined);
    expect(registry.registry).toBe(undefined);
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe('key_1234567890');
    expect(registry.token).toBe(undefined);
    expect(registry.organization).toBe('github');
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe(undefined);
    expect(registry.password).toBe(undefined);
    expect(registry["replaces-base"]).toBe(undefined);

    // hex-repository
    registry = registries[4];
    expect(registry.type).toBe('hex_repository');
    expect(registry.url).toBe('https://private-repo.example.com');
    expect(registry.registry).toBe(undefined);
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe(undefined);
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe('private-repo');
    expect(registry["auth-key"]).toBe('ak_1234567890');
    expect(registry["public-key-fingerprint"]).toBe('pkf_1234567890');
    expect(registry.username).toBe(undefined);
    expect(registry.password).toBe(undefined);
    expect(registry["replaces-base"]).toBe(undefined);

    // maven-repository
    registry = registries[5];
    expect(registry.type).toBe('maven_repository');
    expect(registry.url).toBe('https://artifactory.example.com');
    expect(registry.registry).toBe(undefined);
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe(undefined);
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe('octocat');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry["replaces-base"]).toBe(true);

    // npm-registry
    registry = registries[6];
    expect(registry.type).toBe('npm_registry');
    expect(registry.url).toBe(undefined);
    expect(registry.registry).toBe('npm.pkg.github.com');
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe('tkn_1234567890');
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe(undefined);
    expect(registry.password).toBe(undefined);
    expect(registry["replaces-base"]).toBe(true);

    // nuget-feed
    registry = registries[7];
    expect(registry.type).toBe('nuget_feed');
    expect(registry.url).toBe('https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json');
    expect(registry.registry).toBe(undefined);
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe(undefined);
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe('octocat@example.com');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry["replaces-base"]).toBe(undefined);

    // python-index
    registry = registries[8];
    expect(registry.type).toBe('python_index');
    expect(registry.url).toBe('https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example');
    expect(registry.registry).toBe(undefined);
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe(undefined);
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe('octocat@example.com');
    expect(registry.password).toBe('pwd_1234567890');
    expect(registry["replaces-base"]).toBe(true);

    // rubygems-server
    registry = registries[9];
    expect(registry.type).toBe('rubygems_server');
    expect(registry.url).toBe('https://rubygems.pkg.github.com/octocat/github_api');
    expect(registry.registry).toBe(undefined);
    expect(registry.host).toBe(undefined);
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe('tkn_1234567890');
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe(undefined);
    expect(registry.password).toBe(undefined);
    expect(registry["replaces-base"]).toBe(false);

    // terraform-registry
    registry = registries[10];
    expect(registry.type).toBe('terraform_registry');
    expect(registry.url).toBe(undefined);
    expect(registry.registry).toBe(undefined);
    expect(registry.host).toBe('terraform.example.com');
    expect(registry.key).toBe(undefined);
    expect(registry.token).toBe('tkn_1234567890');
    expect(registry.organization).toBe(undefined);
    expect(registry.repo).toBe(undefined);
    expect(registry["auth-key"]).toBe(undefined);
    expect(registry["public-key-fingerprint"]).toBe(undefined);
    expect(registry.username).toBe(undefined);
    expect(registry.password).toBe(undefined);
    expect(registry["replaces-base"]).toBe(undefined);
  });
});
