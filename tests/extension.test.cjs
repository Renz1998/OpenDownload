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
  alarms: {get: async () => undefined, create: async () => {}, onAlarm: on("alarm")},
  runtime: {id: 'test-extension', onInstalled: on('installed'), onMessage: on('message'), sendNativeMessage: async (host, data) => {
    assert.equal(host, 'org.opendownload.app'); transfers.push(data);
    if (nativeFails) throw new Error('Native host unavailable'); return {ok: true};
  }},
  contextMenus: {removeAll: fn => fn(), create: () => {}, onClicked: on('context')},
  storage: {local: {get: async () => settings, set: async data => Object.assign(settings, data), remove: async key => {for(const entry of Array.isArray(key) ? key : [key]) delete settings[entry];}}},
  action: {setBadgeText: async () => {}, setBadgeBackgroundColor: async () => {}},
  downloads: {download: async options => { assert.equal(options.url,'https://raw.githubusercontent.com/Renz1998/OpenDownload/main/docs/connection-test.txt'); await capture({id:12,url:options.url}); return 12; }, onDeterminingFilename: on('determining'), pause: async id => events.push(['pause', id]), cancel: async id => events.push(['cancel', id]), resume: async id => events.push(['resume', id]), erase: async query => events.push(['erase', query.id])}
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
  nativeFails = false; settings.captureHosts = 'www.pinterest.com';
  await capture({id: 6, url: 'https://i.pinimg.com/originals/example.jpg', referrer: 'https://www.pinterest.com/pin/example/'});
  assert.equal(transfers.at(-1).url, 'https://i.pinimg.com/originals/example.jpg');
  assert.deepEqual(events.slice(-3), [['pause', 6], ['cancel', 6], ['erase', 6]]);
  console.log('PASS website capture includes its separate image CDN through the download referrer');
  await capture({id: 7, url: 'blob:https://www.pinterest.com/example', referrer: 'https://www.pinterest.com/'});
  assert.ok(settings.captureNote.includes('browser-only')); assert.ok(!events.some(event => event[1]===7));
  console.log('PASS browser-only downloads stay in the browser with an explanation');
  await capture({id: 8, url: 'https://unrelated.example/image.jpg', referrer: 'https://unrelated.example/'});
  assert.ok(settings.captureNote.includes('outside')); assert.ok(!events.some(event => event[1]===8));
  console.log('PASS unrelated sites remain excluded with an explanation');
  const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, '../browser-extension/manifest.json')));
  assert.equal(manifest.manifest_version, 3); assert.ok(!manifest.host_permissions); assert.ok(!manifest.permissions.includes('cookies'));
  console.log('PASS MV3 manifest has no broad website or cookie permissions');
  const crypto = require('node:crypto');
  const actualId = [...crypto.createHash('sha256').update(Buffer.from(manifest.key,'base64')).digest().subarray(0,16)].map(byte => String.fromCharCode(97+(byte>>4),97+(byte&15))).join('');
  const identity = JSON.parse(fs.readFileSync(path.join(__dirname,'../browser-extension/identity.json')));
  const appPaths = fs.readFileSync(path.join(__dirname,'../src/OpenDownload.Core/AppPaths.cs'),'utf8');
  assert.equal(actualId,identity.extensionId); assert.ok(appPaths.includes('ExtensionId = "'+actualId+'"'));
  console.log('PASS stable extension key matches app and installer origin');
  nativeFails = false; settings.capture = true; settings.lastError = 'Previous handoff failed';
  await listeners.alarm({name:'opendownload-status'});
  assert.equal(transfers.at(-1).action,'browser-status'); assert.equal(transfers.at(-1).captureEnabled,true); assert.equal(transfers.at(-1).handoffError,true);
  console.log('PASS browser heartbeat reports capture and failed-handoff state');
  settings.capture=false;
  let result=await new Promise(resolve=>listeners.message({type:'capture-test'},{id:'test-extension'},resolve));
  assert.equal(result.ok,false); assert.ok(result.error.includes('Turn on')); console.log('PASS capture test explains the disabled capture setting');
  settings.capture=true; settings.captureHosts='www.pinterest.com';
  result=await new Promise(resolve=>listeners.message({type:'capture-test'},{id:'test-extension'},resolve));
  assert.equal(result.ok,true); assert.deepEqual(events.slice(-3),[['pause',12],['cancel',12],['erase',12]]);
  console.log('PASS user-invoked diagnostic exercises automatic capture despite an unrelated website filter');
  console.log('14 extension tests passed.');
})().catch(error => { console.error(error); process.exitCode = 1; });
