import { readFile, writeFile } from 'fs/promises';
import { console, process } from 'node';
import { join, relative } from 'path';
import * as semver from 'semver';

async function updateTaskJsonFiles(cwd, version) {
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
    contents.version.Patch = version.patch;
    const updated = `${cv.Major}.${cv.Minor}.${cv.Patch}`;

    await writeFile(fileName, JSON.stringify(contents, null, 2) + '\n', 'utf-8');
    console.log(`✅  Updated ${relative(cwd, fileName)} from ${current} → version ${updated}`);
  }
}

async function updateVssExtensions(cwd, version) {
  const fileName = join(cwd, 'vss-extension.json');
  const contents = JSON.parse(await readFile(fileName, 'utf-8'));
  contents.version = version.toString();

  await writeFile(fileName, JSON.stringify(contents, null, 2) + '\n', 'utf-8');
  console.log(`✅  Updated ${relative(cwd, fileName)} → version ${version}`);
}

async function run() {
  const packageJson = JSON.parse(await readFile('package.json', 'utf-8'));
  const version = semver.parse(packageJson.version);
  if (!version) {
    throw new Error('Invalid version in package.json');
  }

  console.log(`Updating versions to ${version}...`);

  await updateTaskJsonFiles(process.cwd(), version);
  await updateVssExtensions(process.cwd(), version);

  console.log('✅  All updates completed successfully.');
}

await run();
