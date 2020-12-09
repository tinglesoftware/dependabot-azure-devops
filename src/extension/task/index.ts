import tl = require('azure-pipelines-task-lib/task');
import tr = require('azure-pipelines-task-lib/toolrunner');


async function run() {
    try {
        // Checking if bundler and ruby are installed
        tl.debug('Checking for bundler install...');
        tl.which('bundler', true);
        tl.debug('Checking for ruby install...');
        tl.which('ruby', true);

        // Install Gem files
        tl.debug("Installing Gem files");
        let bundlerRunner: tr.ToolRunner = tl.tool(tl.which('bundler', true));
        bundlerRunner.arg(['install']);
        bundlerRunner.arg(['-j', '3']);
        bundlerRunner.arg(['--path', 'vendor']);
        bundlerRunner.arg(['--gemfile', 'script/Gemfile']);
        await bundlerRunner.exec();

        // Now execute the dependabot script
        tl.debug("Running update script");
        let scriptRunner: tr.ToolRunner = tl.tool(tl.which('bundler', true));
        scriptRunner.arg(['exec', 'ruby', 'script/update-script.rb']);
        await scriptRunner.exec();
    }
    catch (err) {
        tl.setResult(tl.TaskResult.Failed, err.message);
    }
}

run();
