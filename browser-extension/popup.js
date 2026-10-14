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
el('capture').addEventListener('change', () => chrome.storage.local.set({capture: el('capture').checked}).then(() => { el('status').textContent = el('capture').checked ? 'Automatic capture is on. Website Download buttons can now send supported files to OpenDownload.' : 'Automatic capture is off. Website Download buttons will use your browser.'; }));
el('hosts').addEventListener('change', () => chrome.storage.local.set({captureHosts: el('hosts').value.trim()}));
chrome.storage.local.get(['capture', 'captureHosts', 'lastError', 'captureNote']).then(data => {
  el('capture').checked = !!data.capture; el('hosts').value = data.captureHosts || ''; el('status').textContent = data.lastError || data.captureNote || (!data.capture ? 'Automatic capture is off. Turn it on above to use website Download buttons.' : '');
});
if (new URLSearchParams(location.search).get('setup') === '1') request({action: 'ping'});

el('capture-test').addEventListener('click',async()=>{
  el('capture-test').disabled=true;
  try {
    const result=await chrome.runtime.sendMessage({type:'capture-test'});
    if(!result?.ok) throw new Error(result?.error || 'No reply from the extension.');
    el('status').textContent='Test started. Look in OpenDownload for the capture test and its checksum result. If it stays queued, resume the queue.';
  } catch(error) { el('status').textContent=error.message; }
  finally { el('capture-test').disabled=false; }
});
