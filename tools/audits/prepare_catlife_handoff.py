"""Create the local source/runtime handoff and restore it without copying Library."""
import argparse
import json
from pathlib import Path
import shutil
import zipfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--refresh-source', action='store_true', help='Update the small source snapshot after final source fixes; keep runtime assets and Library.')
    parser.add_argument('--refresh', action='store_true', help='Recreate this task-owned handoff after a dependency fix; restore into the same validation directory.')
    args = parser.parse_args()
    inventory = json.loads(args.inventory.read_text(encoding='utf-8-sig'))
    if args.refresh:
        for entry in inventory['files']:
            if entry['kind'] == 'source': entry['bytes'] = (args.project / entry['path']).stat().st_size
        args.inventory.write_text(json.dumps(inventory, ensure_ascii=False, indent=2), encoding='utf-8')
    args.output.mkdir(parents=True, exist_ok=True)
    restore = args.output / 'restored-project'
    if args.refresh_source:
        entries = [item for item in inventory['files'] if item['kind'] == 'source']
        with zipfile.ZipFile(args.output / 'CatLife-source-snapshot.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            for entry in entries:
                source = args.project / entry['path']
                entry['bytes'] = source.stat().st_size
                archive.write(source, entry['path'])
                target = restore / entry['path']; target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, target)
        args.inventory.write_text(json.dumps(inventory, ensure_ascii=False, indent=2), encoding='utf-8')
        shutil.copyfile(args.inventory, args.output / 'runtime-handoff.json')
        print(json.dumps({'sourceFilesRefreshed': len(entries), 'runtimePackageReused': True}))
        return
    if restore.exists() and not args.refresh:
        raise SystemExit('Restore directory already exists; reuse its results, do not overwrite it.')
    packages = []
    for kind, filename in [('source', 'CatLife-source-snapshot.zip'), ('runtime-asset', 'CatLife-runtime-assets-local.zip')]:
        target = args.output / filename
        if target.exists() and not args.refresh:
            raise SystemExit(f'Package already exists: {target}')
        entries = [item for item in inventory['files'] if item['kind'] == kind]
        with zipfile.ZipFile(target, 'w' if args.refresh else 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            for entry in entries:
                path = args.project / entry['path']
                if path.stat().st_size != entry['bytes']:
                    raise SystemExit(f'Inventory changed; refresh the inventory: {entry["path"]}')
                archive.write(path, entry['path'])
        with zipfile.ZipFile(target) as archive:
            archive.extractall(restore)
        packages.append({'path': str(target), 'files': len(entries), 'bytes': target.stat().st_size})
    shutil.copyfile(args.inventory, args.output / 'runtime-handoff.json')
    report = {'packages': packages, 'restoredProject': str(restore), 'copiedLibrary': False,
              'rights': 'local-only; redistribution not confirmed', 'unity': inventory['unityVersion']}
    (args.output / 'handoff-prepared.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    main()
