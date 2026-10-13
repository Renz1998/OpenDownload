"""Exercise the shipped installer under a named test-results scope only."""
import argparse, json, pathlib, subprocess, uuid
parser=argparse.ArgumentParser(); parser.add_argument('--setup',required=True); parser.add_argument('--root',required=True); args=parser.parse_args()
root=pathlib.Path(args.root).resolve(); setup=pathlib.Path(args.setup).resolve(); scope=root/'test-results'/('installer-'+uuid.uuid4().hex); scope.mkdir(parents=True)
def run(mode,target,success=True):
    p=subprocess.run([str(setup),mode,str(target),'--test-mode'],cwd=root,timeout=120,creationflags=subprocess.CREATE_NO_WINDOW)
    assert (p.returncode==0)==success, f'{mode} returned {p.returncode}'
target=scope/'app'
run('--install',target)
assert (target/'OpenDownload.Desktop.exe').exists() and (target/'installed-files.json').exists()
assert json.loads((target/'native-host.json').read_text())['allowed_origins']==['chrome-extension://oagdmhfnjjcidibdhafcbjmfmaeaojam/']
print('PASS fresh installation and exact browser origin registration file')
(target/'data-path.txt').write_text(str(scope/'personal-data'))
(target/'my-unrelated-file.txt').write_text('keep')
run('--install',target)
assert (target/'data-path.txt').read_text()==str(scope/'personal-data')
assert list(scope.glob('app.backup-*'))
assert (target/'my-unrelated-file.txt').read_text()=='keep'
print('PASS upgrade preserves data-folder pointer and previous program backup')
manifest=target/'installed-files.json'; original=manifest.read_text(); entries=json.loads(original); entries.append('my-unrelated-file.txt'); manifest.write_text(json.dumps(entries))
run('--uninstall',target,False); assert (target/'my-unrelated-file.txt').read_text()=='keep'
manifest.write_text(original)
print('PASS tampered install manifest cannot delete an unrelated file')
(target/'my-unrelated-file.txt').write_text('keep')
run('--uninstall',target)
assert (target/'my-unrelated-file.txt').read_text()=='keep' and not (target/'OpenDownload.Desktop.exe').exists()
print('PASS uninstall removes only recorded application files and keeps unrelated files')
other=scope/'other'; other.mkdir(); (other/'important.txt').write_text('keep')
run('--install',other,False)
assert (other/'important.txt').read_text()=='keep'
print('PASS non-application folder refused without changing its files')
run('--install',root/'outside-installer-tests',False)
assert not (root/'outside-installer-tests').exists()
print('PASS test-mode path boundary refuses operations outside test-results')
print('6 installer lifecycle checks passed.')
