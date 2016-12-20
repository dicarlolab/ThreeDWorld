import h5py
import numpy as np
from PIL import Image
import os
import sys

datapath = sys.argv[1]
start = int(sys.argv[2])
end = int(sys.argv[3])
outdir = sys.argv[4]

if not os.path.exists(outdir):
    os.mkdir(outdir)

h = h5py.File(datapath, 'r')
images = h.get('images')

for i in range(start, end):
    image = Image.fromarray(images[i])
    image.save(os.path.join(outdir, 'img_%06d.jpg' % i))
