"""Build the portable ZIP and content-addressed component archives for GitHub Releases.

Usage: python scripts/package_release.py artifacts/VoxLab-win-x64 artifacts/release 0.2.0
No credentials; no installer; never include Data in a release.
"""
import hashlib
import json
from pathlib import Path
import re
import sys
import zipfile


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def component(path):
    if path.startswith('App/Drivers/'):
        return 'drivers'
    if not path.startswith('App/') or path.startswith(('App/VoxLab.', 'App/Web/', 'App/docs/', 'App/licenses/')) or path in (
        'App/Update-VoxLab.ps1', 'App/README.md', 'App/LICENSE', 'App/THIRD-PARTY-NOTICES.md'
    ):
        return 'app'
    return 'runtime'


def archive(destination, root, paths):
    with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as output:
        for path in paths:
            info = zipfile.ZipInfo(path, date_time=(2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            output.writestr(info, (root / path).read_bytes(), compresslevel=9)


def main():
    root, destination, version = Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve(), sys.argv[3]
    if not re.fullmatch(r'\d+\.\d+\.\d+', version):
        raise ValueError('Use a stable three-part version')
    if root == destination or root in destination.parents:
        raise ValueError('Output must be outside the portable directory')
    destination.mkdir(parents=True, exist_ok=True)
    paths = sorted(p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()
                   and p.relative_to(root).parts[0] != 'Data' and p.name != '.update.lock')
    if not {'VoxLab.exe', 'App/VoxLab.dll', 'App/Update-VoxLab.ps1'}.issubset(paths):
        raise ValueError('Incomplete clean portable build')
    if any((root / p).is_symlink() for p in paths):
        raise ValueError('Do not package symbolic links')
    files = [{'path': p, 'sha256': sha(root / p), 'length': (root / p).stat().st_size, 'component': component(p)} for p in paths]
    bundles = []
    for name in ('app', 'runtime', 'drivers'):
        entries = [f for f in files if f['component'] == name]
        fingerprint = hashlib.sha256(json.dumps(entries, ensure_ascii=False, sort_keys=True).encode('utf-8')).hexdigest()[:16]
        asset = f'VoxLab-{name}-{fingerprint}.zip'
        target = destination / asset
        archive(target, root, [f['path'] for f in entries])
        bundles.append({'id': name, 'asset': asset, 'sha256': sha(target), 'size': target.stat().st_size})
    manifest = {'format': 1, 'version': version, 'files': files, 'bundles': bundles}
    (destination / 'VoxLab-update.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    portable = destination / f'VoxLab-{version}-win-x64-portable.zip'
    archive(portable, root, paths)
    assets = [portable, destination / 'VoxLab-update.json', *[destination / b['asset'] for b in bundles]]
    (destination / 'SHA256SUMS.txt').write_text(''.join(f'{sha(p)}  {p.name}\n' for p in assets), encoding='utf-8')
    print(f'{len(files)} files, {len(bundles)} update components, portable ZIP {portable.stat().st_size / 1048576:.1f} MiB')
    for b in bundles:
        print(f"  {b['id']}: {b['size'] / 1048576:.1f} MiB")


if __name__ == '__main__':
    main()
