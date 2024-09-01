import { setResult, TaskResult } from "azure-pipelines-task-lib/task"
import { debug, warning, error } from "azure-pipelines-task-lib/task"

async function run() {
  try {
    
    // TODO: This...
    setResult(TaskResult.Succeeded);

  }
  catch (e: any) {
      error(`Unhandled exception: ${e}`);
      setResult(TaskResult.Failed, e?.message);
  }
}

run();
