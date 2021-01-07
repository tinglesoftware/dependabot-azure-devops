# Running using a Kubernetes CronJob

Kubernetes CronJobs are useful tools for running repeated jobs on a schedule. For more information on them read the [documentation](https://kubernetes.io/docs/concepts/workloads/controllers/cron-jobs/).
Using the Docker version of tools, we can create a CronJob and have it run periodically. The [environment variables](#environment-variables) discussed above are supplied in the job template but can be stored in a [ConfigMap](https://kubernetes.io/docs/concepts/configuration/configmap/) for ease of reuse.

Use the [template provided](./dependabot-template.yml) and replace the parameters in curly braces (e.g. replace `{{AZURE_ORGANIZATION}}` with the actual value for your organization), then deploy it. Be sure to replace the `{{CRON_SCHEDULE}}` variable with the desired schedule as per the [Cron format](https://en.wikipedia.org/wiki/Cron).

Notes:

1. Timezone support is not yet available in Kubernetes ([Issue 1](https://github.com/kubernetes/kubernetes/issues/47202), [Issue 2](https://github.com/kubernetes/kubernetes/issues/78795)). If this is important to you, consider using [cronjobber](https://github.com/hiddeco/cronjobber).
2. History for successful and failed jobs is restricted to 1 (change to suit you).
3. Jobs are removed after 2 days (`ttlSecondsAfterFinished: 172800`). No need keep it for too long.
4. Jobs run duration is capped at 1 hour (`activeDeadlineSeconds: 3600`). Need to conserve resources.
5. Labels can be used to find cronjobs created.
6. Annotations can be used to store extra data for comparison but not searching/finding e.g. package ecosystem.
