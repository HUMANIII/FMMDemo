"""Loopback-only multipart/CDN fixture. No production credentials or service access.
python Tools/remote_test_server.py --root .tools/remote-server --port 18084
"""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
from email.parser import BytesParser
from email.policy import default
from urllib.parse import urlsplit, parse_qs
import argparse
import json
import time

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--port', type=int, default=18084)
args = parser.parse_args()
root = args.root.resolve()
(root / 'content').mkdir(parents=True, exist_ok=True)

class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(root), **kw)

    def record(self, **data):
        with (root / 'requests.jsonl').open('a', encoding='utf-8') as stream:
            stream.write(json.dumps({'time': time.time(), **data}) + '\n')

    def log_message(self, *args):
        pass

    def do_GET(self):
        self.record(method='GET', path=self.path)
        if self.path == '/health':
            self.send_response(200); self.end_headers(); self.wfile.write(b'FMV fixture'); return
        super().do_GET()

    def do_POST(self):
        url = urlsplit(self.path)
        if url.path != '/upload':
            self.send_error(404); return
        authorized = self.headers.get('x-upload-password') == 'fmv-local-verification'
        data = self.rfile.read(int(self.headers.get('Content-Length', 0)))
        content_type = self.headers.get('Content-Type', '')
        message = BytesParser(policy=default).parsebytes(('Content-Type: ' + content_type + '\r\n\r\n').encode() + data)
        parts = list(message.iter_parts()) if message.is_multipart() else []
        valid = len(parts) == 1 and parts[0].get_param('name', header='content-disposition') == 'file'
        filename = parts[0].get_filename() if valid else ''
        if not filename or filename != Path(filename).name or '/' in filename or '\\' in filename:
            valid = False
        fail = parse_qs(url.query).get('fail', [''])[0]
        status = 403 if not authorized else 400 if not valid else 500 if fail and fail in filename else 200
        payload = parts[0].get_payload(decode=True) if valid else b''
        if status == 200:
            (root / 'content' / filename).write_bytes(payload)
        self.record(method='POST', filename=filename, field='file' if valid else '', authorized=authorized, status=status, bytes=len(payload))
        self.send_response(status); self.end_headers(); self.wfile.write(str(status).encode())

print(f'FMV verification server: http://127.0.0.1:{args.port}', flush=True)
ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()
