"""Pair the two actual render views, retain frame labels, and encode at 60fps."""
from pathlib import Path
import subprocess
import sys
from PIL import Image, ImageDraw

folder = Path(sys.argv[1])
ffmpeg = sys.argv[2]
paired = folder / 'paired'
paired.mkdir(exist_ok=True)
files = sorted((folder / 'front').glob('frame-*.png'))
for path in files:
    with Image.open(path) as front, Image.open(folder/'rear'/path.name) as rear:
        canvas = Image.new('RGB',(1280,592),'#e8e5dd')
        canvas.paste(front,(0,32))
        canvas.paste(rear,(640,32))
        ImageDraw.Draw(canvas).text((12,10),f'{folder.name} | F{int(path.stem[6:]):03d} | FRONT / REAR | Blender source, not APP',fill='#202020')
        canvas.save(paired/path.name)
for start in range(0,len(files),24):
    sheet = Image.new('RGB',(1920,1356),'#e8e5dd')
    for offset,path in enumerate(files[start:start+24]):
        with Image.open(paired/path.name) as picture:
            sheet.paste(picture.resize((480,222)),(offset%4*480,offset//4*226))
    sheet.save(folder/f'contact-{start//24+1:02d}.png')
subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-n','-framerate','60',
                '-start_number','1','-i',str(paired/'frame-%03d.png'),
                '-c:v','libx264','-crf','18','-pix_fmt','yuv420p',
                str(folder/'turn-material.mp4')],check=True)
print(f'Packaged {len(files)} actual source frames at 60fps: {folder}')
