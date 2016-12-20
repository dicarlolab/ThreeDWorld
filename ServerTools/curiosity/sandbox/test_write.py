import numpy as np
import time
import os
import zmq
import struct
import json


print "starting"

ctx = zmq.Context()
sock = ctx.socket(zmq.REQ)

print "connecting"

sock.connect("tcp://18.93.15.188:23042")

print "connected"

sock.send(json.dumps({'n': 4, 'msg': {"msg_type": "CLIENT_JOIN"}}))

print "join"

times = []

for i in range(30):
    t0 = time.time()
    msg = json.loads(sock.recv())
    print msg
    img0 = sock.recv()
    print len(img0)
    img1 = sock.recv()
    print len(img1)
    img2 = sock.recv()
    print len(img2)
    t1 = time.time()
    f0 = os.path.join('testims', 'im%d.png' % i)
    with open(f0, 'w') as _f:
        _f.write(img2)
    actions = []
    msg = {}
    msg['msg'] = {
        "msg_type": "CLIENT_INPUT",
        "sendSceneInfo": False,
        "get_obj_data": False,
        "actions": [
            {
                "id": "59",
                "vel": [1,1,1],
                "ang_vel": [1,1,1],
            },
        ]}
    msg['n'] = 4
    sock.send_json(msg)
    t2 = time.time()
    print('round %d: %f %f:' % (i, t1 - t0, t2 - t1))
    times.append(t1 - t0)

print(np.mean(times))

