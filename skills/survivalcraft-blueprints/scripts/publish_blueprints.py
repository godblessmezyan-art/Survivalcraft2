"""Publish validated Survivalcraft JSON to Digital Study; preserve and verify exact bytes."""
import argparse
import base64
import getpass
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE = 'https://wxhappylife.top/digital-study'
PUBLIC = '/api/game-blueprints/v1/blueprints'
ADMIN = '/api/admin/game-blueprints/v1/blueprints'
MAX_BYTES = 2 * 1024 * 1024
FIELDS = ('name', 'schemaVersion', 'kind', 'size', 'blockCount', 'byteLength', 'sha256')


class PublishError(Exception):
    pass


def private_credentials(path):
    """Decrypt only the user's explicitly configured, local current-user DPAPI file."""
    if os.name != 'nt':
        raise PublishError('This saved credential requires the Windows account that encrypted it.')
    import ctypes
    from ctypes import wintypes
    document = json.loads(Path(path).read_text(encoding='utf-8-sig'))
    if document.get('protection') != 'windows-dpapi-current-user':
        raise PublishError('Unsupported credential protection format.')
    encrypted = base64.b64decode(document['ciphertext'], validate=True)

    class Blob(ctypes.Structure):
        _fields_ = [('size', wintypes.DWORD), ('data', ctypes.POINTER(ctypes.c_ubyte))]

    buffer = (ctypes.c_ubyte * len(encrypted)).from_buffer_copy(encrypted)
    source, decoded = Blob(len(encrypted), buffer), Blob()
    crypt = ctypes.WinDLL('crypt32', use_last_error=True)
    crypt.CryptUnprotectData.argtypes = [ctypes.POINTER(Blob), ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(Blob)]
    crypt.CryptUnprotectData.restype = wintypes.BOOL
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.LocalFree.argtypes = [ctypes.c_void_p]
    kernel.LocalFree.restype = ctypes.c_void_p
    if not crypt.CryptUnprotectData(ctypes.byref(source), None, None, None, None, 1, ctypes.byref(decoded)):
        raise PublishError('Cannot decrypt the local credential with this Windows account.')
    try:
        value = json.loads(ctypes.string_at(decoded.data, decoded.size).decode('utf-8'))
        if not isinstance(value.get('username'), str) or not isinstance(value.get('password'), str):
            raise PublishError('Incomplete local credential.')
        return value
    finally:
        ctypes.memset(decoded.data, 0, decoded.size)
        kernel.LocalFree(ctypes.cast(decoded.data, ctypes.c_void_p))


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise PublishError('HTTP redirect refused; check the configured Digital Study address.')


class Client:
    def __init__(self, token=None):
        self.token = token
        self.opener = urllib.request.build_opener(NoRedirect())

    def request(self, path, method='GET', data=None, content_type=None, admin=False, raw=False):
        headers = {'Accept': 'application/json', 'Accept-Encoding': 'identity', 'Cache-Control': 'no-cache'}
        if admin:
            if not self.token:
                raise PublishError('Administrator credentials missing. Set DIGITAL_STUDY_ADMIN_TOKEN or use --login.')
            headers['Authorization'] = 'Bearer ' + self.token
        if content_type:
            headers['Content-Type'] = content_type
        request = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
        try:
            with self.opener.open(request, timeout=20) as response:
                body = response.read(MAX_BYTES + 1)
                if len(body) > MAX_BYTES:
                    raise PublishError('Response exceeds 2 MiB.')
                if raw:
                    return body
                return json.loads(body.decode('utf-8'))
        except urllib.error.HTTPError as error:
            body = error.read(4096).decode('utf-8', errors='replace')
            if self.token:
                body = body.replace(self.token, '[redacted]')
            raise PublishError(f'HTTP {error.code} {method} {path}: {body}') from None

    def json(self, path, value):
        return self.request(path, 'POST', json.dumps(value).encode('utf-8'), 'application/json', admin=True)

    def file(self, path, source, raw, fields=None):
        boundary = 'Blueprint-' + uuid.uuid4().hex
        # The server validates an extension only; an ASCII wire filename avoids locale conversion.
        parts = []
        for key, value in (fields or {}).items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode())
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="blueprint.json"\r\nContent-Type: application/json\r\n\r\n'.encode())
        parts.extend([raw, f'\r\n--{boundary}--\r\n'.encode()])
        return self.request(path, 'POST', b''.join(parts), f'multipart/form-data; boundary={boundary}', admin=True)

    def catalog(self, admin=False):
        items = []
        for page in range(1, 101):
            payload = self.request((ADMIN if admin else PUBLIC) + f'?page={page}&pageSize=50', admin=admin)
            if payload.get('apiVersion') != 1 or payload.get('page') != page or not isinstance(payload.get('items'), list):
                raise PublishError('Unexpected blueprint catalog contract.')
            items.extend(payload['items'])
            if len(items) >= payload['total']:
                return items
        raise PublishError('Catalog exceeds the bounded scan; use a specific existing ID after inspection.')


def load_item(source, receipt):
    path, proof = Path(source).resolve(), json.loads(Path(receipt).read_text(encoding='utf-8-sig'))
    raw = path.read_bytes()
    if path.suffix.lower() != '.json' or not 0 < len(raw) <= MAX_BYTES or raw.startswith(b'\xef\xbb\xbf'):
        raise PublishError('Upload needs a 1-byte to 2-MiB UTF-8 JSON file without BOM.')
    root = json.loads(raw.decode('utf-8'))
    if proof.get('status') != 'PASS' or proof.get('validator') != 'SCIEPort.BlueprintImportService + material capability catalog':
        raise PublishError('A successful actual SCIEPort importer/material validation receipt is required.')
    sha = hashlib.sha256(raw).hexdigest()
    if proof.get('sha256') != sha or proof.get('byteLength') != len(raw):
        raise PublishError('JSON changed after validation; regenerate the validation receipt.')
    for field in ('name', 'kind', 'schemaVersion'):
        if proof.get(field) != root.get(field):
            raise PublishError('Validation receipt does not describe this JSON: ' + field)
    if proof['kind'] != 'builderassistant.blueprint' or proof['schemaVersion'] != 3:
        raise PublishError('New publications must use blueprint schema 3.')
    if proof['blockCount'] > 10000 or any(proof['size'][axis] > 64 for axis in 'xyz'):
        raise PublishError('The verified website contract currently permits 10000 cells and 64 per axis. Split into aligned modules.')
    if any(p.get('templateId', '').startswith('Ancient') for p in root.get('furniturePlacements', [])):
        raise PublishError('Website only knows ten legacy templates. Export expanded cells with -PortableOutput first.')
    return path, raw, proof


def match(meta, proof, require_published=False):
    if meta.get('apiVersion') != 1:
        raise PublishError('Unexpected metadata API version.')
    for field in FIELDS:
        if meta.get(field) != proof[field]:
            raise PublishError('Server/local metadata mismatch: ' + field)
    if require_published and meta.get('status') != 'published':
        raise PublishError('Blueprint is not publicly published.')


def validate_id(value):
    if not isinstance(value, str) or not re.fullmatch(r'[a-f0-9]{8}-[a-f0-9]{4}-4[a-f0-9]{3}-[89ab][a-f0-9]{3}-[a-f0-9]{12}', value):
        raise PublishError('Server returned an invalid UUID v4.')
    return value


def verify_public(client, row, raw, proof):
    identity = validate_id(row.get('id'))
    revision = row.get('revision')
    if type(revision) is not int or revision < 1:
        raise PublishError('Invalid content revision.')
    detail = client.request(PUBLIC + '/' + identity)
    match(detail, proof, require_published=True)
    if detail['revision'] != revision:
        raise PublishError('Revision changed during publication; refresh before continuing.')
    path = PUBLIC + f'/{identity}/json?revision={revision}'
    downloaded = client.request(path, raw=True)
    if downloaded != raw or hashlib.sha256(downloaded).hexdigest() != proof['sha256']:
        raise PublishError('Public download is not byte-identical to the validated JSON.')
    if not any(x.get('id') == identity and x.get('sha256') == proof['sha256'] for x in client.catalog()):
        raise PublishError('Published blueprint missing from the public game download catalog.')
    return {'id': identity, 'revision': revision, 'name': proof['name'], 'sha256': proof['sha256'],
            'downloadUrl': BASE + path, 'pageUrl': BASE + '/index.html#blueprints',
            'status': 'published', 'publicDownloadVerified': True, 'modVersion': proof['modVersion']}


def publish(client, source, raw, proof, checkpoint, update_id=None):
    # All mutations use the normal authenticated application API. No database writes or auth bypass.
    public = client.catalog()
    same = next((x for x in public if x.get('sha256') == proof['sha256']), None)
    if same and (not update_id or same.get('id') == update_id):
        return verify_public(client, same, raw, proof)
    if not client.token:
        raise PublishError('Administrator credentials missing; no upload was attempted.')
    me = client.request('/api/auth/me', admin=True)
    if me.get('role') != 'admin':
        raise PublishError('The authenticated account is not a Digital Study administrator.')
    match(client.file(ADMIN + '/validate', source, raw), proof)
    catalog = client.catalog(admin=True)
    same = next((x for x in catalog if x.get('sha256') == proof['sha256']), None)
    if update_id:
        identity = validate_id(update_id)
        row = client.request(ADMIN + '/' + identity, admin=True)
        checkpoint('update_requested', {'id': identity, 'revision': row['revision']})
        row = client.file(ADMIN + '/' + identity + '/json', source, raw, {'expectedRevision': row['revision']})
    elif same:
        row = same
    else:
        if any(x.get('name') == proof['name'] for x in catalog):
            raise PublishError('A different blueprint already has this name. Inspect its ID/revision before explicitly updating it.')
        checkpoint('create_requested', {'name': proof['name'], 'sha256': proof['sha256']})
        row = client.file(ADMIN, source, raw)
    match(row, proof)
    identity = validate_id(row.get('id'))
    checkpoint('draft_saved', {'id': identity, 'revision': row['revision'], 'sha256': proof['sha256']})
    if row['status'] != 'published':
        row = client.json(ADMIN + '/' + identity + '/publish', {'expectedRevision': row['revision']})
    match(row, proof, require_published=True)
    checkpoint('published_pending_readback', {'id': identity, 'revision': row['revision']})
    return verify_public(client, row, raw, proof)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--item', nargs=2, action='append', required=True, metavar=('JSON', 'VALIDATION_RECEIPT'))
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--dry-run', action='store_true', help='Validate local receipts and list matching public entries; no authentication or mutations.')
    parser.add_argument('--login', action='store_true', help='Prompt locally for existing account credentials, keep token in process memory only.')
    parser.add_argument('--credential-file', type=Path, help='Explicitly configured local current-user DPAPI credential; never a plaintext password file.')
    parser.add_argument('--update-id', help='Explicitly update one inspected existing blueprint; requires exactly one item.')
    args = parser.parse_args()
    if args.update_id and len(args.item) != 1:
        parser.error('--update-id requires exactly one --item')
    args.report.parent.mkdir(parents=True, exist_ok=True)
    record = {'site': BASE, 'dryRun': args.dry_run, 'items': [], 'status': 'pending'}

    def checkpoint(stage, details):
        record['stage'] = stage
        record['lastOperation'] = details
        args.report.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    try:
        items = [load_item(*item) for item in args.item]
        client = Client(os.environ.get('DIGITAL_STUDY_ADMIN_TOKEN'))
        installed = Path(os.environ.get('CODEX_HOME', str(Path.home()/'.codex')))/'skills/survivalcraft-blueprints/private/credentials.dpapi.json'
        adjacent = Path(__file__).resolve().parents[1]/'private/credentials.dpapi.json'
        saved = args.credential_file or (adjacent if adjacent.exists() else installed)
        if not args.dry_run and (args.login or (not client.token and saved.exists())):
            credentials = {'username': input('Digital Study admin username: '), 'password': getpass.getpass('Password (not saved): ')} if args.login else private_credentials(saved)
            username, password = credentials['username'], credentials['password']
            payload = client.request('/api/auth/login', 'POST', json.dumps({'username': username, 'password': password}).encode('utf-8'), 'application/json')
            password = None
            credentials.clear()
            if payload.get('role') != 'admin' or not isinstance(payload.get('token'), str):
                raise PublishError('Administrator login was not obtained.')
            client.token = payload['token']
        if args.dry_run:
            catalog = client.catalog()
            record['items'] = [{'name': proof['name'], 'sha256': proof['sha256'], 'blockCount': proof['blockCount'],
                                'matchingPublishedIds': [x['id'] for x in catalog if x.get('sha256') == proof['sha256']]} for _, _, proof in items]
            record['status'] = 'ready_for_authenticated_upload'
        else:
            for source, raw, proof in items:
                record['items'].append(publish(client, source, raw, proof, checkpoint, args.update_id))
                checkpoint('verified', {'completed': len(record['items'])})
            record['status'] = 'published_and_download_verified'
        checkpoint('complete', {'completed': len(record['items'])})
        print(json.dumps(record, ensure_ascii=False, indent=2))
    except Exception as error:
        record['status'] = 'incomplete'
        record['error'] = str(error)
        record['failedStage'] = record.get('stage', 'local_validation')
        record['lastKnownOperation'] = record.get('lastOperation')
        checkpoint('stopped', {'completed': len(record['items'])})
        print('Publication stopped: ' + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    raise SystemExit(main())
