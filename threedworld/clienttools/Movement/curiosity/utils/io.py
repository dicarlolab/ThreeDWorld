from StringIO import StringIO

from PIL import Image
import numpy as np
import zmq
import os

def send_array(socket, A, flags=0, copy=True, track=False):
    """send a numpy array with metadata"""
    md = dict(
        dtype = str(A.dtype),
        shape = A.shape,
    )
    socket.send_json(md, flags|zmq.SNDMORE)
    return socket.send(A, flags, copy=copy, track=track)


def recv_array(socket, flags=0, copy=True, track=False):
  """recv a numpy array"""
  md = socket.recv_json(flags=flags)
  msg = socket.recv(flags=flags, copy=copy, track=track)
  buf = buffer(msg)
  A = np.frombuffer(buf, dtype=md['dtype'])
  return A.reshape(md['shape'])


def handle_message(sock, write=False, outdir='', imtype='png', prefix=''):
    # Handle info
    info = sock.recv()
    print("got message")
    # Handle first set of images from camera 1
    nstr = sock.recv()
    narray2 = np.asarray(Image.open(StringIO(nstr)).convert('RGB'))
    ostr = sock.recv()
    oarray2 = np.asarray(Image.open(StringIO(ostr)).convert('RGB'))
    imstr = sock.recv()
    imarray2 = np.asarray(Image.open(StringIO(imstr)).convert('RGB'))
    # Handle second set of images from camera 2
    nstr = sock.recv()
    narray = np.asarray(Image.open(StringIO(nstr)).convert('RGB'))
    ostr = sock.recv()
    oarray = np.asarray(Image.open(StringIO(ostr)).convert('RGB'))
    imstr = sock.recv()
    imarray = np.asarray(Image.open(StringIO(imstr)).convert('RGB'))

    im = Image.fromarray(imarray)
    im2 = Image.fromarray(imarray2)
    imo = Image.fromarray(oarray)
    if write:
        if not os.path.exists(outdir):
            os.mkdir(outdir)
        im.save(os.path.join(outdir, 'image_%s.%s' % (prefix, imtype)))
        im2.save(os.path.join(outdir, '2image_%s.%s' % (prefix, imtype)))
        imo.save(os.path.join(outdir, 'objects_%s.%s' % (prefix, imtype)))
        #with open(os.path.join(outdir, 'image_%s.%s' % (prefix, imtype)), 'w') as _f:
        #    _f.write(imstr)
        #with open(os.path.join(outdir, 'objects_%s.%s' % (prefix, imtype)), 'w') as _f:
        #    _f.write(ostr)
        #with open(os.path.join(outdir, 'normals_%s.%s' % (prefix, imtype)), 'w') as _f:
        #    _f.write(nstr)
        if '_0' in prefix:
            with open(os.path.join(outdir, 'info_%s.json' % prefix), 'w') as _f:
                _f.write(info)
    return [info, narray, oarray, imarray]
