import uuid
import zmq
import json
import subprocess
import socket
import pymongo
from bson.objectid import ObjectId
#import psutil
import os
from tabulate import tabulate
import datetime
import signal
import sys
#import fcntl
import struct
from optparse import OptionParser
import time
import numpy as np

if __name__ == "__main__":

    parser = OptionParser()
    parser.add_option("-p", "--port", dest="portn", default =5555, type=int)

    (options, args) = parser.parse_args()

    conn = pymongo.MongoClient(port=22334)
    coll = conn['synthetic_generative']['3d_models']

    port = str(options.portn)
    context = zmq.Context()
    socket_self = context.socket(zmq.REP)

    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.connect(("google.com",80))

    host_address = s.getsockname()[0]

    s.close()

    print "host: " + host_address + " with port: " + port
    socket_self.bind("tcp://%s:%s" % (host_address, port))

    #default_inquery     = {'type': 'shapenet', 'version': 2, 'has_texture':True, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}
    default_inquery     = {'type': 'shapenetremat', 'has_texture': True, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}
    CACHE = {}
    default_keys = ['boundb_pos', 'isLight', 'anchor_type', 'aws_address', 'complexity', 'center_pos']

    def correct(record):
        if '_id' in record:
            # Used for string of _id
            record['_id_str'] = str(record.pop('_id'))
        if 'aws_version' not in record:
            # Use this for update aws file and caching it correctly
            record['aws_version']   = '0'

    while True:
        #  Wait for next request from client
        print "Waiting for info: "
        message = socket_self.recv_json()
        print "Received request: ", message
        #time.sleep (1)  
        #time.sleep(0.1)
        #test_dict   = {"test": 0}

        return_list = []
        #return_dict = {"test": 0}
        for key_value in message:
            now_request = message[key_value]
            q = now_request.get('find_argu', default_inquery)
            for _k in default_keys:
                if _k not in q:
                    q[_k] = {'$exists': True}
            if not str(q) in CACHE:
                idvals = np.array([str(_x['_id']) for _x in list(coll.find(q, projection=['_id']))])
                CACHE[str(q)] = idvals
                print('new', q, len(idvals))
            idvals = CACHE[str(q)] 

            num_ava = len(idvals)
            if now_request['choose_mode'] == 'random':
                rng = np.random.RandomState(seed=now_request['choose_argu'].get('seed', 0))
                goodidinds = rng.permutation(num_ava)[: now_request['choose_argu']['number']] 
                goodidvals = idvals[goodidinds]
            elif now_request['choose_mode'] == 'all':
                goodidvals = idvals
            goodidvals = map(ObjectId, goodidvals) 
            return_list0 = list(coll.find({'_id': {'$in': goodidvals}}, projection=default_keys))
            map(correct, return_list0)
            return_list.extend(return_list0)
        
        return_dict = dict(enumerate(return_list))
        print('Returning %d items' % len(return_dict))
        socket_self.send_json(return_dict)
