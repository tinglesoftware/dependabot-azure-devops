/* eslint-disable no-undef */

import { readFile, writeFile } from 'fs/promises';
import { join, relative } from 'path';
import * as semver from 'semver';

async function updateTaskJsonFiles({cwd, dev, version, buildNumber}) {
  const fileNames = [
    // 'tasks/dependabotV1/task.json',
    'tasks/dependabotV2/task.json',
  ].map((fileName) => join(cwd, fileName));

  for (const fileName of fileNames) {
    const contents = JSON.parse(await readFile(fileName, 'utf-8'));
    const cv = (contents.version ||= {});
    const current = `${cv.Major}.${cv.Minor}.${cv.Patch}`;
    // we do not update the Major version because ideally it would be a different task
    // contents.version.Major = version.major;
    contents.version.Minor = version.minor;
    // if dev, override patch with BUILD_NUMBER
    contents.version.Patch = dev ? buildNumber : version.patch;
    const updated = `${cv.Major}.${cv.Minor}.${cv.Patch}`;

    await writeFile(fileName, JSON.stringify(contents, null, 2) + '\n', 'utf-8');
    console.log(`✅  Updated ${relative(cwd, fileName)} from ${current} → version ${updated}`);
  }
}

async function updateVssExtensions({cwd, dev, version, buildNumber}) {
  const fileName = join(cwd, 'vss-extension.json');
  const contents = JSON.parse(await readFile(fileName, 'utf-8'));
  contents.version = `${version.major}.${version.minor}.${version.patch}.${buildNumber}`;

  await writeFile(fileName, JSON.stringify(contents, null, 2) + '\n', 'utf-8');
  console.log(`✅  Updated ${relative(cwd, fileName)} → version ${contents.version}`);
}

async function run(dev) {
  const packageJson = JSON.parse(await readFile('package.json', 'utf-8'));
  let version = semver.parse(packageJson.version);
  if (!version) {
    throw new Error('Invalid version in package.json');
  }

  const buildNumber = Number.parseInt(process.env['BUILD_NUMBER'] || '0');
  console.log(`Updating versions to ${version} and BuildNumber: ${buildNumber}`);

  var opt = {cwd: process.cwd(), dev, version, buildNumber};
  await updateTaskJsonFiles(opt);
  await updateVssExtensions(opt);

  console.log('✅  All updates completed successfully.');
}

await run(process.argv.includes('--dev'));
