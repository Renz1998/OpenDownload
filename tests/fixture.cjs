const http = require('node:http');
const crypto = require('node:crypto');
const payload = Buffer.alloc(4 * 1024 * 1024 + 123);
for (let i = 0; i < payload.length; i++) payload[i] = (i * 17 + Math.floor(i / 256)) % 251;
let changed = false;
let drops = 0;
const handler = (req, res) => {
  const path = req.url.split('?')[0];
  if (path === '/meta') { res.end(JSON.stringify({sha256: crypto.createHash('sha256').update(payload).digest('hex'), size: payload.length})); return; }
  if (path === '/api/view' || path === '/image-mislabel') {
    const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aN1sAAAAASUVORK5CYII=', 'base64');
    res.writeHead(200, {'Content-Type': path === '/api/view' ? 'image/png' : 'image/jpeg', 'Content-Length': png.length}); res.end(png); return;
  }
  if (path === '/mutate') { changed = true; res.end('ok'); return; }
  if (path === '/redirect') { res.writeHead(302, {Location: '/range'}); res.end(); return; }
  if (path === '/cross-auth') { res.writeHead(302, {Location: `http://127.0.0.1:${secondary.address().port}/range?no-auth`}); res.end(); return; }
  if (req.url.includes('?no-auth') && (req.headers.authorization || req.headers.cookie)) { res.writeHead(403); res.end(); return; }
  if (path === '/empty') { res.writeHead(416, {'Content-Range': 'bytes */0'}); res.end(); return; }
  if (path === '/auth' && req.headers.authorization !== 'Bearer test-token') { res.writeHead(401); res.end(); return; }
  if (path === '/forbidden') { res.writeHead(403); res.end(); return; }
  const etag = path === '/change' && changed ? '"v2"' : '"v1"';
  const useRange = !['/ignore', '/unknown', '/novalidator'].includes(path) && req.headers.range && (!req.headers['if-range'] || req.headers['if-range'] === etag);
  let start = 0, end = payload.length - 1;
  const headers = {'Content-Type': 'application/octet-stream', 'Accept-Ranges': 'bytes'};
  if (path === '/named') headers['Content-Disposition'] = "attachment; filename*=UTF-8''report%20final.zip";
  if (path !== '/novalidator') headers.ETag = etag;
  if (useRange) {
    const match = /bytes=(\d+)-(\d*)/.exec(req.headers.range);
    start = Number(match[1]); end = match[2] ? Number(match[2]) : end;
    if (start >= payload.length || end >= payload.length) { res.writeHead(416, {'Content-Range': `bytes */${payload.length}`}); res.end(); return; }
    headers['Content-Range'] = `bytes ${path === '/bad' || (path === '/badlate' && end > 0) ? start + 1 : start}-${end}/${payload.length}`;
  }
  if (path !== '/unknown') headers['Content-Length'] = end - start + 1;
  res.writeHead(useRange ? 206 : 200, headers);
  let cursor = start;
  const interval = setInterval(() => {
    if (cursor > end) { clearInterval(interval); res.end(); return; }
    const count = Math.min(32768, end - cursor + 1);
    res.write(payload.subarray(cursor, cursor + count)); cursor += count;
    if (path === '/drop' && end > 1 && drops++ < 2 && cursor - start >= 65536) { clearInterval(interval); res.destroy(); }
  }, path === '/slow' || path === '/change' ? 12 : 1);
  res.on('close', () => clearInterval(interval));
};
const server = http.createServer(handler);
const secondary = http.createServer(handler);
secondary.listen(0, '127.0.0.1', () => server.listen(0, '127.0.0.1', () => console.log(server.address().port)));
