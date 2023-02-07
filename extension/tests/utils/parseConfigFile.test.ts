import { load } from "js-yaml";
import * as fs from "fs";
import * as path from "path";
import { parseRegistries } from "../../task/utils/parseConfigFile";

describe("Parse registries", () => {
  it("Should replace hyphen with underscore", () => {
    let config: any = load(fs.readFileSync('tests/utils/sample-registries-1.yml', "utf-8"));
    let registries = parseRegistries(config);
    expect(registries.length).toBe(11)
    expect(registries[0].type).toBe('composer_repository')
    expect(registries[1].type).toBe('docker_registry')
    expect(registries[2].type).toBe('git')
    expect(registries[3].type).toBe('hex_organization')
    expect(registries[4].type).toBe('hex_repository')
    expect(registries[5].type).toBe('maven_repository')
    expect(registries[6].type).toBe('npm_registry')
    expect(registries[7].type).toBe('nuget_feed')
    expect(registries[8].type).toBe('python_index')
    expect(registries[9].type).toBe('rubygems_server')
    expect(registries[10].type).toBe('terraform_registry')
  });
});
