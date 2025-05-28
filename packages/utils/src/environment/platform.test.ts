import { expect, test } from 'vitest';
import { getPlatform } from './platform';

test('getPlatform should return "azure_app_service" when WEBSITE_HOSTNAME is set', () => {
  process.env.WEBSITE_HOSTNAME = 'example.azurewebsites.net';
  expect(getPlatform()).toBe('azure_app_service');
  delete process.env.WEBSITE_HOSTNAME;
});

test('getPlatform should return "azure_container_apps" when CONTAINER_APP_ENV_DNS_SUFFIX is set', () => {
  process.env.CONTAINER_APP_ENV_DNS_SUFFIX = 'example.com';
  expect(getPlatform()).toBe('azure_container_apps');
  delete process.env.CONTAINER_APP_ENV_DNS_SUFFIX;
});

test('getPlatform should return "azure_static_web_app" when WEBSITE_STATICWEBAPP_RESOURCE_ID is set', () => {
  process.env.WEBSITE_STATICWEBAPP_RESOURCE_ID = 'resource_id';
  expect(getPlatform()).toBe('azure_static_web_app');
  delete process.env.WEBSITE_STATICWEBAPP_RESOURCE_ID;
});

test('getPlatform should return "cloudflare_pages" when CF_PAGES_URL is set', () => {
  process.env.CF_PAGES_URL = 'https://pages.cloudflare.com';
  expect(getPlatform()).toBe('cloudflare_pages');
  delete process.env.CF_PAGES_URL;
});

test('getPlatform should return "vercel" when VERCEL_BRANCH_URL is set', () => {
  process.env.VERCEL_BRANCH_URL = 'https://vercel.com';
  expect(getPlatform()).toBe('vercel');
  delete process.env.VERCEL_BRANCH_URL;
});

test('getPlatform should return "local" when no environment variables are set', () => {
  expect(getPlatform()).toBe(undefined);
});

test('getPlatform should return "azure_static_web_app" when WEBSITE_HOSTNAME and WEBSITE_STATICWEBAPP_RESOURCE_ID are set', () => {
  process.env.WEBSITE_HOSTNAME = 'example.azurewebsites.net';
  process.env.WEBSITE_STATICWEBAPP_RESOURCE_ID = 'resource_id';
  expect(getPlatform()).toBe('azure_static_web_app');
  delete process.env.WEBSITE_HOSTNAME;
  delete process.env.WEBSITE_STATICWEBAPP_RESOURCE_ID;
});
