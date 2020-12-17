# Hosting dependabot

The goal here is to have a service that pickups the changes in the `.github/dependabot.yml` file and creates jobs at specified times/intervals hence achieve somethings close to the native GitHub dependabot implementation.

Ideally, this would run on Kubernetes and would incur some operational costs. Sponsorship may be required or the project may be taken up by the Azure DevOps team. Self hosting option would also work.

Currently, we have an implementation that works internally and are yet to share it publicly because it is very much a work in progress.
