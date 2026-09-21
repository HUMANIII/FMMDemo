"""Decode every placeholder to EOF and record the shipped media format/hashes."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import imageio_ffmpeg

root = Path(__file__).resolve().parents[1]
records = []
for path in sorted((root / 'Assets/FMV/Content/Videos').glob('*.mp4')):
    run = subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), '-hide_banner', '-i', str(path),
                          '-map', '0:v:0', '-f', 'null', '-'], capture_output=True, text=True, check=True)
    log = run.stderr
    video_line = next(line.strip() for line in log.splitlines() if 'Video: h264' in line)
    frames = int(re.findall(r'frame=\s*(\d+)', log)[-1])
    assert '1280x720' in video_line and '30 fps' in video_line and 'bt709' in video_line, video_line
    assert 'Audio:' not in log and frames == 90 and 'Duration: 00:00:03.00' in log, log
    records.append(dict(file=str(path.relative_to(root)).replace('\\', '/'), frames=frames,
                        seconds=3, width=1280, height=720, fps=30, audio=False,
                        codec=video_line, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
assert len(records) == 5
out = root / 'Docs/Evidence/media.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'PASS: {len(records)} MP4 files decoded to frame 90; report: {out}')
