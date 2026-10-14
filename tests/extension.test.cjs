const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const listeners = {};
const transfers = [];
const events = [];
let settings = {};
let nativeFails = false;
const on = key => ({addListener: fn => {listeners[key] = fn;}});
const chrome = {
  runtime: {id: 'test-extension', onInstalled: on('installed'), onMessage: on('message'), sendNativeMessage: async (host, data) => {
    assert.equal(host, 'org.opendownload.app'); transfers.push(data);
    if (nativeFails) throw new Error('Native host unavailable'); return {ok: true};
  }},
  contextMenus: {removeAll: fn => fn(), create: () => {}, onClicked: on('context')},
  storage: {local: {get: async () => settings, set: async data => Object.assign(settings, data), remove: async key => {delete settings[key];}}},
  action: {setBadgeText: async () => {}, setBadgeBackgroundColor: async () => {}},
  downloads: {onDeterminingFilename: on('determining'), pause: async id => events.push(['pause', id]), cancel: async id => events.push(['cancel', id]), resume: async id => events.push(['resume', id]), erase: async query => events.push(['erase', query.id])}
};
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../browser-extension/background.js'), 'utf8'), {chrome, URL, Set, Error});
async function capture(item) { await new Promise(resolve => assert.equal(listeners.determining(item, resolve), true)); }
(async () => {
  listeners.installed();
  await listeners.context({menuItemId: 'download-link', linkUrl: 'https://example.com/file.zip'});
  assert.equal(transfers.length, 1); console.log('PASS extension context-menu handoff');
  await listeners.context({menuItemId: 'download-link', linkUrl: 'file:///C:/test'});
  assert.equal(transfers.length, 1); console.log('PASS extension rejects non-HTTP URLs');
  await capture({id: 1, url: 'https://example.com/file.zip'});
  assert.equal(events.length, 0); console.log('PASS automatic capture is off by default');
  settings.capture = true; settings.captureHosts = 'example.com';
  await capture({id: 2, url: 'https://other.example/file.zip'});
  await capture({id: 3, url: 'https://example.com/file.zip', incognito: true});
  assert.equal(events.length, 0); console.log('PASS capture host filter and incognito exclusion');
  await capture({id: 4, url: 'https://example.com/file.zip'});
  assert.deepEqual(events, [['pause', 4], ['cancel', 4], ['erase', 4]]); console.log('PASS accepted capture holds save, cancels transfer and erases only its cancelled history');
  nativeFails = true;
  await capture({id: 5, url: 'https://example.com/file.zip'});
  assert.deepEqual(events.slice(-2), [['pause', 5], ['resume', 5]]); console.log('PASS failed capture resumes original browser transfer');
  const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, '../browser-extension/manifest.json')));
  assert.equal(manifest.manifest_version, 3); assert.ok(!manifest.host_permissions); assert.ok(!manifest.permissions.includes('cookies'));
  console.log('PASS MV3 manifest has no broad website or cookie permissions');
  const crypto = require('node:crypto');
  const actualId = [...crypto.createHash('sha256').update(Buffer.from(manifest.key,'base64')).digest().subarray(0,16)].map(byte => String.fromCharCode(97+(byte>>4),97+(byte&15))).join('');
  const identity = JSON.parse(fs.readFileSync(path.join(__dirname,'../browser-extension/identity.json')));
  const appPaths = fs.readFileSync(path.join(__dirname,'../src/OpenDownload.Core/AppPaths.cs'),'utf8');
  assert.equal(actualId,identity.extensionId); assert.ok(appPaths.includes('ExtensionId = "'+actualId+'"'));
  console.log('PASS stable extension key matches app and installer origin');
  console.log('8 extension tests passed.');
})().catch(error => { console.error(error); process.exitCode = 1; });
