# Running using a Kubernetes CronJob

Kubernetes CronJobs are useful tools for running repeated jobs on a schedule. For more information on them read the [documentation](https://kubernetes.io/docs/concepts/workloads/controllers/cron-jobs/).
Using the Docker version of tools, we can create a CronJob and have it run periodically. The [environment variables](#environment-variables) discussed above are supplied in the job template but can be stored in a [ConfigMap](https://kubernetes.io/docs/concepts/configuration/configmap/) for ease of reuse.

Use the [template provided](./dependabot-template.yml) and replace the parameters in curly braces (e.g. replace `{{AZURE_ORGANIZATION}}` with the actual value for your organization), then deploy it. Be sure to replace the `{{CRON_SCHEDULE}}` variable with the desired schedule as per the [Cron format](https://en.wikipedia.org/wiki/Cron) An example would like:

```yml
apiVersion: batch/v1beta1
kind: CronJob
metadata:
  name: dependabot-{{AZURE_PROJECT}}-{{AZURE_REPOSITORY}}
  labels:
    tingle.io/dependabot: 'true'
  annotations:
    project: '{{AZURE_PROJECT}}'
    repository: '{{AZURE_REPOSITORY}}'
spec:
  schedule: '{{CRON_SCHEDULE}}' # (See https://en.wikipedia.org/wiki/Cron for format)
  successfulJobsHistoryLimit: 1
  failedJobsHistoryLimit: 1
  jobTemplate:
    metadata:
      labels:
        tingle.io/dependabot: 'true'
      annotations:
        project: '{{AZURE_PROJECT}}'
        repository: '{{AZURE_REPOSITORY}}'
    spec:
      backoffLimit: 1
      ttlSecondsAfterFinished: 172800
      template:
        spec:
          restartPolicy: OnFailure
          activeDeadlineSeconds: 3600
          containers:
          - name: dependabot
            image: 'tingle/dependabot-azure-devops:0.1.1'
            env:
              - name: GITHUB_ACCESS_TOKEN
                value: '{{GITHUB_ACCESS_TOKEN}}'
              - name: AZURE_HOSTNAME
                value: '{{AZURE_HOSTNAME}}'
              - name: AZURE_ACCESS_TOKEN
                value: '{{AZURE_ACCESS_TOKEN}}'
              - name: AZURE_ORGANIZATION
                value: '{{AZURE_ORGANIZATION}}'
              - name: AZURE_PROJECT
                value: '{{AZURE_PROJECT}}'
              - name: AZURE_REPOSITORY
                value: '{{AZURE_REPOSITORY}}'
              - name: DEPENDABOT_PACKAGE_MANAGER
                value: '{{DEPENDABOT_PACKAGE_MANAGER}}'
              - name: DEPENDABOT_DIRECTORY
                value: '{{DEPENDABOT_DIRECTORY_PATH}}'
              - name: DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT
                value: '{{DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT}}'
              - name: DEPENDABOT_TARGET_BRANCH
                value: '{{DEPENDABOT_TARGET_BRANCH}}'
              - name: DEPENDABOT_VERSIONING_STRATEGY
                value: '{{DEPENDABOT_VERSIONING_STRATEGY}}'
              - name: DEPENDABOT_EXTRA_CREDENTIALS
                value: '{{DEPENDABOT__EXTRA_CREDENTIALS}}'
              - name: DEPENDABOT_ALLOW
                value: '{{DEPENDABOT_ALLOW}}'
              - name: DEPENDABOT_IGNORE
                value: '{{DEPENDABOT_IGNORE}}'
```
