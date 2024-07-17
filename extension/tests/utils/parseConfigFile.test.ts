import { load } from "js-yaml";
import * as fs from "fs";
import * as path from "path";
import { parseRegistries, parseUpdates, validateConfiguration } from "../../task/utils/parseConfigFile";
import { IDependabotRegistry, IDependabotUpdate } from "../../task/IDependabotConfig";

describe("Parse configuration file", () => {
  it("Parsing works as expected", () => {
    let config: any = load(fs.readFileSync('tests/utils/dependabot.yml', "utf-8"));
    let updates = parseUpdates(config);
    expect(updates.length).toBe(3);

    // first
    const first = updates[0];
    expect(first.directory).toBe('/');
    expect(first.directories).toEqual([]);
    expect(first.packageEcosystem).toBe('docker');
    expect(first.insecureExternalCodeExecution).toBe(undefined);
    expect(first.registries).toEqual([]);

    // second
    const second = updates[1];
    expect(second.directory).toBe('/client');
    expect(second.directories).toEqual([]);
    expect(second.packageEcosystem).toBe('npm');
    expect(second.insecureExternalCodeExecution).toBe('deny');
    expect(second.registries).toEqual(['reg1', 'reg2']);

    // third
    const third = updates[2];
    expect(third.directory).toBe(undefined);
    expect(third.directories).toEqual(['/src/client', '/src/server']);
    expect(third.packageEcosystem).toBe('nuget');
    expect(third.groups).toBe('{\"microsoft\":{\"patterns\":[\"microsoft*\"],\"update-types\":[\"minor\",\"patch\"]}}');
  });
});

describe("Parse registries", () => {
  it("Parsing works as expected", () => {
    let config: any = load(fs.readFileSync('tests/utils/sample-registries.yml', "utf-8"));
    let registries = parseRegistries(config);
    expect(Object.keys(registries).length).toBe(11);

    // composer-repository
    var registry = registries['composer'];
    expect(registry.type).toBe('composer_repository');
    expect(registry.url).toBe('https://repo.packagist.com/example-company/');
    expect(registry["index-url"]).toBe(undefined);
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
    registry = registries['dockerhub'];
    expect(registry.type).toBe('docker_registry');
    expect(registry.url).toBe(undefined);
    expect(registry["index-url"]).toBe(undefined);
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
    registry = registries['github-octocat'];
    expect(registry.type).toBe('git');
    expect(registry.url).toBe('https://github.com');
    expect(registry["index-url"]).toBe(undefined);
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
    registry = registries['github-hex-org'];
    expect(registry.type).toBe('hex_organization');
    expect(registry.url).toBe(undefined);
    expect(registry["index-url"]).toBe(undefined);
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
    registry = registries['github-hex-repository'];
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
    registry = registries['maven-artifactory'];
    expect(registry.type).toBe('maven_repository');
    expect(registry.url).toBe('https://artifactory.example.com');
    expect(registry["index-url"]).toBe(undefined);
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
    registry = registries['npm-github'];
    expect(registry.type).toBe('npm_registry');
    expect(registry.url).toBe(undefined);
    expect(registry["index-url"]).toBe(undefined);
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
    registry = registries['nuget-azure-devops'];
    expect(registry.type).toBe('nuget_feed');
    expect(registry.url).toBe('https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json');
    expect(registry["index-url"]).toBe(undefined);
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
    registry = registries['python-azure'];
    expect(registry.type).toBe('python_index');
    expect(registry.url).toBe(undefined);
    expect(registry["index-url"]).toBe('https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example');
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
    registry = registries['ruby-github'];
    expect(registry.type).toBe('rubygems_server');
    expect(registry.url).toBe('https://rubygems.pkg.github.com/octocat/github_api');
    expect(registry["index-url"]).toBe(undefined);
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
    registry = registries['terraform-example'];
    expect(registry.type).toBe('terraform_registry');
    expect(registry.url).toBe(undefined);
    expect(registry["index-url"]).toBe(undefined);
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

describe("Validate registries", () => {
  it("Validation works as expected", () => {
    // let config: any = load(fs.readFileSync('tests/utils/dependabot.yml', "utf-8"));
    // let updates = parseUpdates(config);
    // expect(updates.length).toBe(2);

    var updates: IDependabotUpdate[] = [
      {
        packageEcosystem: "npm",
        directory: "/",
        registries: ["dummy1", "dummy2"],
      },
    ];

    var registries: Record<string, IDependabotRegistry> = {
      'dummy1': {
        type: 'nuget',
        url: "https://pkgs.dev.azure.com/contoso/_packaging/My_Feed/nuget/v3/index.json",
        token: "pwd_1234567890",
      },
      'dummy2': {
        type: "python-index",
        url: "https://pkgs.dev.azure.com/octocat/_packaging/my-feed/pypi/example",
        username: "octocat@example.com",
        password: "pwd_1234567890",
        "replaces-base": true,
      },
    };

    // works as expected
    validateConfiguration(updates, registries);

    // fails: registry not referenced
    updates[0].registries = [];
    expect(() => validateConfiguration(updates, registries)).toThrow(`Registries: 'dummy1,dummy2' have not been referenced by any update`);

    // fails: registrynot configured
    updates[0].registries = ["dummy1", "dummy2", "dummy3",];
    expect(() => validateConfiguration(updates, registries)).toThrow(`Referenced registries: 'dummy3' have not been configured in the root of dependabot.yml`);
  });
});
