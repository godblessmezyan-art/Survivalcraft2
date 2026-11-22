"""No live mutations: exercise publication safety and recovery with a controlled service."""
import importlib.util
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('publisher', ROOT/'skills/survivalcraft-blueprints/scripts/publish_blueprints.py')
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)
ID = '11111111-1111-4111-8111-111111111111'
RAW = b'{"schemaVersion":3,"kind":"builderassistant.blueprint","name":"sample","cells":[{"x":0,"y":0,"z":0,"key":"GraniteBlock"}]}'
PROOF = dict(status='PASS', validator='SCIEPort.BlueprintImportService + material capability catalog', name='sample', schemaVersion=3,
             kind='builderassistant.blueprint', size=dict(x=1,y=1,z=1), blockCount=1, byteLength=len(RAW), sha256=hashlib.sha256(RAW).hexdigest(), modVersion='0.89.0.0')


class Service:
    token = 'test-credential'
    def __init__(self):
        self.row = None
        self.creates = self.publishes = 0
        self.corrupt = self.bad_metadata = self.timeout_after_save = False

    def metadata(self):
        return {'apiVersion':1, **{k:PROOF[k] for k in p.FIELDS}, 'id':ID, 'revision':1, 'status':'draft'}

    def catalog(self, admin=False):
        return [self.row.copy()] if self.row and (admin or self.row['status']=='published') else []

    def request(self, path, admin=False, raw=False):
        if path == '/api/auth/me':
            return {'role':'admin'}
        if raw:
            return b'corrupted' if self.corrupt else RAW
        return self.row.copy()

    def file(self, path, source, raw, fields=None):
        self.assert_bytes = raw
        if path.endswith('/validate'):
            data = self.metadata()
            if self.bad_metadata:
                data['blockCount'] = 2
            return data
        self.creates += 1
        self.row = self.metadata()
        if self.timeout_after_save:
            self.timeout_after_save = False
            raise TimeoutError('Outcome uncertain after server save')
        return self.row.copy()

    def json(self, path, value):
        if path != p.ADMIN+'/'+ID+'/publish' or value != {'expectedRevision':1}:
            raise AssertionError('Wrong publication ID/revision')
        self.publishes += 1
        self.row['status'] = 'published'
        return self.row.copy()


class PublicationTests(unittest.TestCase):
    def publish(self, service):
        return p.publish(service, Path('sample.json'), RAW, PROOF, lambda *_: None)

    def test_create_publish_public_readback(self):
        service = Service()
        result = self.publish(service)
        self.assertEqual((service.creates, service.publishes), (1,1))
        self.assertTrue(result['publicDownloadVerified'])
        self.assertEqual(service.assert_bytes, RAW)
        self.assertEqual(result['sha256'], PROOF['sha256'])

    def test_repeat_is_idempotent_without_credentials(self):
        service = Service()
        first = self.publish(service)
        service.token = None
        second = self.publish(service)
        self.assertEqual(first, second)
        self.assertEqual((service.creates, service.publishes), (1,1))

    def test_uncertain_create_is_not_blindly_retried(self):
        service = Service(); service.timeout_after_save = True
        with self.assertRaises(TimeoutError):
            self.publish(service)
        self.assertEqual(service.creates, 1)
        result = self.publish(service)
        self.assertTrue(result['publicDownloadVerified'])
        self.assertEqual(service.creates, 1)

    def test_preflight_mismatch_stops_before_draft(self):
        service = Service(); service.bad_metadata = True
        with self.assertRaises(p.PublishError):
            self.publish(service)
        self.assertEqual((service.creates, service.publishes), (0,0))

    def test_corrupt_public_download_is_failure(self):
        service = Service(); service.corrupt = True
        with self.assertRaises(p.PublishError):
            self.publish(service)

    def test_missing_identity_does_not_attempt_upload(self):
        service = Service(); service.token = None
        with self.assertRaises(p.PublishError):
            self.publish(service)
        self.assertEqual((service.creates, service.publishes), (0,0))

    def test_changed_json_invalidates_receipt(self):
        with tempfile.TemporaryDirectory() as temp:
            source, receipt = Path(temp)/'sample.json', Path(temp)/'proof.json'
            source.write_bytes(RAW); receipt.write_text(json.dumps(PROOF), encoding='utf-8')
            p.load_item(source, receipt)
            source.write_bytes(RAW+b'\n')
            with self.assertRaises(p.PublishError):
                p.load_item(source, receipt)

    def test_redirect_refuses_credential_forwarding(self):
        with self.assertRaises(p.PublishError):
            p.NoRedirect().redirect_request(None,None,302,'Redirect',{},'https://other.example')


if __name__ == '__main__':
    unittest.main()
