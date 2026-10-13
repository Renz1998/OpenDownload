"""End-to-end test of the published host, named pipe, desktop queue and encrypted DB.
Only launch against a dedicated disposable test data directory, never a personal profile.
"""
import argparse, hashlib, json, os, pathlib, sqlite3, struct, subprocess, time

parser = argparse.ArgumentParser()
parser.add_argument("--app", required=True)
parser.add_argument("--data", required=True)
parser.add_argument("--node", default="node")
parser.add_argument("--root", required=True)
args = parser.parse_args()
app = pathlib.Path(args.app)
data = pathlib.Path(args.data)
data.mkdir(parents=True, exist_ok=True)
download = data / "downloads"
download.mkdir(exist_ok=True)
if (data / "downloads.db").exists():
    raise RuntimeError("Use a fresh dedicated smoke-test data directory")
db = sqlite3.connect(data / "downloads.db")
db.execute("CREATE TABLE jobs (id TEXT PRIMARY KEY, payload BLOB NOT NULL)")
db.execute("CREATE TABLE settings (id INTEGER PRIMARY KEY, payload TEXT NOT NULL)")
db.execute("INSERT INTO settings VALUES (1,?)", (json.dumps({"DownloadDirectory": str(download), "Connections": 4, "ConcurrentDownloads": 2, "MinimizeToTray": False}),))
db.commit(); db.close()
env = dict(os.environ, OPENDOWNLOAD_DATA=str(data))
fixture = subprocess.Popen([args.node, str(pathlib.Path(args.root)/"tests"/"fixture.cjs")], stdout=subprocess.PIPE, text=True, creationflags=subprocess.CREATE_NO_WINDOW)
desktop = subprocess.Popen([str(app / "OpenDownload.Desktop.exe")], env=env)
try:
    port = int(fixture.stdout.readline())
    def request(value):
        payload = json.dumps(value).encode()
        result = subprocess.run([str(app / "OpenDownload.NativeHost.exe")], input=struct.pack("<I", len(payload))+payload, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=30, env=env, creationflags=subprocess.CREATE_NO_WINDOW)
        if len(result.stdout) < 4: raise RuntimeError("No native host reply")
        size = struct.unpack("<I", result.stdout[:4])[0]
        return json.loads(result.stdout[4:4+size])
    for attempt in range(30):
        time.sleep(.3)
        reply = request({"action": "ping"})
        if reply.get("ok"): break
    assert reply.get("ok"), reply
    print("PASS native length framing and desktop pipe ping")
    assert request({"action": "download", "url": f"http://127.0.0.1:{port}/range", "fileName": "native-smoke.bin"}).get("ok")
    target = download / "native-smoke.bin"
    for attempt in range(100):
        if target.exists(): break
        time.sleep(.2)
    assert target.exists(), "Desktop queue did not produce the completed file"
    import urllib.request
    with urllib.request.urlopen(f"http://127.0.0.1:{port}/meta") as response: meta = json.load(response)
    assert hashlib.sha256(target.read_bytes()).hexdigest() == meta["sha256"]
    print("PASS host -> pipe -> desktop -> segmented download -> exact SHA-256")
    assert not request({"action": "download", "url": "file:///C:/Windows/notepad.exe"}).get("ok")
    print("PASS invalid native URL rejected")
    malformed = subprocess.run([str(app / "OpenDownload.NativeHost.exe")], input=struct.pack("<I", 999999), capture_output=True, timeout=10, creationflags=subprocess.CREATE_NO_WINDOW)
    assert malformed.returncode != 0 and not malformed.stdout
    print("PASS oversized native message rejected")
    assert desktop.poll() is None, "Desktop exited unexpectedly"
    print("PASS published self-contained desktop remains running")
finally:
    # Terminate only the exact child processes created by this test.
    desktop.terminate(); desktop.wait(timeout=10)
    fixture.terminate(); fixture.wait(timeout=10)
