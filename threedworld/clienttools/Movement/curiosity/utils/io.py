try:
    from StringIO import StringIO
except ImportError:
    from io import BytesIO as StringIO

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


def handle_message(sock, msg_names, \
        write=False, outdir='', imtype='png', prefix=''):
    # Handle info
    info = sock.recv()
    print("got message")
    data = {'info': info}
    # Iterate over all cameras
    for cam in range(len(msg_names)):
        for n in range(len(msg_names[cam])):
            # Handle set of images per camera
            imgstr = sock.recv()
            imgarray = np.asarray(Image.open(StringIO(imgstr)).convert('RGB'))
            field_name = msg_names[cam][n] + str(cam+1)
            assert field_name not in data, \
                    ('duplicate message name %s' % field_name)
            data[field_name] = imgarray

    #im = Image.fromarray(data['images1'])
    #im2 = Image.fromarray(data['images2'])
    #imo = Image.fromarray(data['objects1'])
    #dim = Image.fromarray(data['accelerations2'])
    #print(outdir, prefix, imtype)
    #imo.save(os.path.join('/Users/damian/Desktop/test_images/new/', 'jerk_%s.%s' % (prefix, imtype)))
    #dim.save(os.path.join('/Users/damian/Desktop/test_images/new/', 'acc_%s.%s' % (prefix, imtype)))
    #im2.save(os.path.join('/Users/damian/Desktop/test_images/new/', 'vel_%s.%s' % (prefix, imtype)))

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
    return data
