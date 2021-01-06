# Running using a Kubernetes CronJob

Kubernetes CronJobs are useful tools for running repeated jobs on a schedule. For more information on them read the [documentation](https://kubernetes.io/docs/concepts/workloads/controllers/cron-jobs/).
Using the Docker version of tools, we can create a CronJob and have it run periodically. The [environment variables](#environment-variables) discussed above are supplied in the job template but can be stored in a [ConfigMap](https://kubernetes.io/docs/concepts/configuration/configmap/) for ease of reuse.

Use the [template provided](./dependabot-template.yml) and replace the parameters in curly braces (e.g. replace `{{ORGANIZATION}}` with the actual value for your organization), then deploy it. Be sure to replace the `{{CRON_SCHEDULE}}` variable with the desired schedule as per the [Cron format](https://en.wikipedia.org/wiki/Cron) An example would like:

```yml
apiVersion: batch/v1beta1
kind: CronJob
metadata:
  name: dependabot-oss-ado-dp
  labels:
    tingle.io/dependabot: 'true'
  annotations:
    project: 'oss'
    repository: 'ado-dp'
spec:
  schedule: '0 2 * * *' # 2 am GMT, every day
  jobTemplate:
    metadata:
      labels:
        tingle.io/dependabot: 'true'
      annotations:
        project: 'oss'
        repository: 'ado-dp'
    spec:
      template:
        spec:
          containers:
          - name: dependabot
            image: 'tingle/dependabot-azure-devops:0.1.1'
            env:
              - name: ORGANIZATION
                value: 'tinglesoftware'
              - name: PROJECT
                value: 'ado'
              - name: REPOSITORY
                value: 'ado-dp'
              - name: PACKAGE_MANAGER
                value: 'nuget'
              - name: SYSTEM_ACCESSTOKEN
                value: 'abcd...efgh'
              - name: GITHUB_ACCESS_TOKEN
                value: 'ijkl..mnop'
              - name: PRIVATE_FEED_NAME
                value: 'tinglesoftware'
              - name: DIRECTORY
                value: '/'
              - name: TARGET_BRANCH
                value: 'master'
              - name: VERSIONING_STRATEGY
                value: '{{VERSIONING_STRATEGY}}'
              - name: AZURE_HOSTNAME
                value: 'dev.azure.com'
              - name: EXTRA_CREDENTIALS
                value: '[{\"type\":\"npm_registry\",\"token\":\"<redacted>\",\"registry\":\"npm.fontawesome.com\"}]'
              - name: OPEN_PULL_REQUESTS_LIMIT
                value: '10'
          restartPolicy: OnFailure

```
