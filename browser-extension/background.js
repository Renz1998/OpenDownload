const HOST = 'org.opendownload.app';
const MAX_URL = 16384;
const pending = new Set();
function validUrl(value) {
  try { const url = new URL(value); return value.length <= MAX_URL && ['http:', 'https:'].includes(url.protocol) && !url.username && !url.password; }
  catch { return false; }
}
async function send(request) {
  if (request.action !== 'ping' && !validUrl(request.url)) throw new Error('Enter an HTTP or HTTPS download URL.');
  const reply = await chrome.runtime.sendNativeMessage(HOST, request);
  if (!reply?.ok) throw new Error(reply?.error || 'OpenDownload could not accept this download.');
  return reply;
}
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({id: 'download-link', title: 'Download with OpenDownload', contexts: ['link', 'video', 'audio', 'image']});
    chrome.contextMenus.create({id: 'download-selection', title: 'Download selected URLs with OpenDownload', contexts: ['selection']});
  });
});
chrome.contextMenus.onClicked.addListener(async info => {
  try {
    if (info.menuItemId === 'download-selection') {
      const urls = [...new Set((info.selectionText || '').split(/\s+/).filter(validUrl))].slice(0, 200);
      if (!urls.length) throw new Error('Select one or more complete HTTP/HTTPS URLs.');
      for (const url of urls) await send({action: 'download', url, referer: info.pageUrl});
    } else await send({action: 'download', url: info.linkUrl || info.srcUrl, referer: info.pageUrl});
    await chrome.action.setBadgeText({text: ''});
    await chrome.storage.local.remove('lastError');
  } catch (error) {
    await chrome.action.setBadgeText({text: '!'});
    await chrome.action.setBadgeBackgroundColor({color: '#b42318'});
    await chrome.storage.local.set({lastError: error.message});
  }
});
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id) return false;
  if (message.type === 'send') {
    send(message.request).then(value => respond({ok: true, ...value})).catch(error => respond({ok: false, error: error.message}));
    return true;
  }
});
// Interception is explicit opt-in. Leave browser downloads alone if handoff fails.
chrome.downloads.onCreated.addListener(async item => {
  const {capture = false, captureHosts = ''} = await chrome.storage.local.get(['capture', 'captureHosts']);
  if (!capture || item.incognito || !validUrl(item.finalUrl || item.url) || pending.has(item.id)) return;
  const url = item.finalUrl || item.url;
  const hosts = captureHosts.split(/[\s,]+/).filter(Boolean).map(s => s.toLowerCase());
  if (hosts.length && !hosts.includes(new URL(url).hostname.toLowerCase())) return;
  // Avoid signed-in downloads: version 0.1 does not automatically extract cookies.
  pending.add(item.id);
  try {
    await chrome.downloads.pause(item.id);
    await send({action: 'download', url, fileName: item.filename ? item.filename.split(/[\\/]/).pop() : undefined, referer: item.referrer});
    await chrome.downloads.cancel(item.id);
  } catch (error) {
    try { await chrome.downloads.resume(item.id); } catch { /* Download may already have finished. */ }
    await chrome.storage.local.set({lastError: error.message});
  } finally { pending.delete(item.id); }
});
