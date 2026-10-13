const el = id => document.getElementById(id);
async function request(value) {
  el('download').disabled = true; el('status').textContent = 'Connecting…';
  try {
    const result = await chrome.runtime.sendMessage({type: 'send', request: value});
    if (!result?.ok) throw new Error(result?.error || 'No reply from OpenDownload.');
    el('status').textContent = value.action === 'ping' ? 'Connected to OpenDownload.' : 'Added to OpenDownload.';
  } catch (error) { el('status').textContent = error.message; }
  finally { el('download').disabled = false; }
}
el('download').addEventListener('click', () => request({action: 'download', url: el('url').value.trim()}));
el('test').addEventListener('click', () => request({action: 'ping'}));
el('url').addEventListener('keydown', event => { if (event.key === 'Enter') el('download').click(); });
el('capture').addEventListener('change', () => chrome.storage.local.set({capture: el('capture').checked}));
el('hosts').addEventListener('change', () => chrome.storage.local.set({captureHosts: el('hosts').value.trim()}));
chrome.storage.local.get(['capture', 'captureHosts', 'lastError']).then(data => {
  el('capture').checked = !!data.capture; el('hosts').value = data.captureHosts || ''; el('status').textContent = data.lastError || '';
});
if (new URLSearchParams(location.search).get('setup') === '1') request({action: 'ping'});
