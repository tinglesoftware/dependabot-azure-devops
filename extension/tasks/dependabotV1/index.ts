import * as tl from 'azure-pipelines-task-lib/task';

tl.setResult(
  tl.TaskResult.Failed,
  'This task version is deprecated and is no longer function. Please use version 2 or later.',
);
