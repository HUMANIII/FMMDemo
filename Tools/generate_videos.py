"""Rebuild the five silent 3-second video placeholders from the supplied artwork.

python -m pip install imageio-ffmpeg==0.6.0
python Tools/generate_videos.py
"""
from pathlib import Path
import subprocess
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'SourceArt' / 'Placeholders'
OUT = ROOT / 'Assets' / 'FMV' / 'Content' / 'Videos'

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    for name in ('wake', 'walk', 'computer', 'return', 'sleep'):
        # Encode a subtle camera push; originals remain unchanged.
        subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y',
            '-loop', '1', '-i', str(ART / f'{name}.png'),
            '-vf', "scale=2560:-2,zoompan=z='1+on*0.00035':x='iw/2-iw/zoom/2':y='ih/2-ih/zoom/2':d=1:s=1280x720:fps=30,setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709",
            '-frames:v', '90', '-an', '-c:v', 'libx264', '-preset', 'medium',
            '-crf', '22', '-profile:v', 'baseline', '-bf', '0', '-pix_fmt', 'yuv420p',
            '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709',
            '-movflags', '+faststart',
            str(OUT / f'{name}.mp4')], check=True)
        print(f'{name}: 1280x720, 30fps, 90 frames, H.264, silent')

if __name__ == '__main__':
    main()
