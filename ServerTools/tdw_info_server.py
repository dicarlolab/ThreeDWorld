import uuid
import zmq
import json
import subprocess
import socket
import pymongo
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
    test_coll = coll.find(default_inquery)
    # Download all the default information
    default_coll    = list(test_coll[:])

    def inc_one_item(test_coll, return_dict, indx_tmp):
        now_indx_dict   = len(return_dict)
        return_dict[now_indx_dict]   = test_coll[indx_tmp]
        if '_id' in return_dict[now_indx_dict]:
            # Used for string of _id
            return_dict[now_indx_dict]['_id_str']       = str(return_dict[now_indx_dict].pop('_id'))
        if 'aws_version' not in return_dict[now_indx_dict]:
            # Use this for update aws file and caching it correctly
            return_dict[now_indx_dict]['aws_version']   = '0'

    while True:
        #  Wait for next request from client
        print "Waiting for info: "
        message = socket_self.recv_json()
        print "Received request: ", message
        #time.sleep (1)  
        #time.sleep(0.1)
        #test_dict   = {"test": 0}

        return_dict     = {}
        #return_dict     = {"test": 0}
        for key_value in message:
            now_request     = message[key_value]
            if (not type(now_request['find_argu']) is dict) and (now_request['find_argu'] == 'default'):
                print("Using default inquery!")
                #test_coll = coll.find(default_inquery)
                test_coll   = default_coll
            else:
                test_coll = coll.find(now_request['find_argu'])
                test_coll       = list(test_coll[:])

            num_ava         = len(test_coll)
            if now_request['choose_mode']=='all':
                for indx_tmp in range(num_ava):
                    inc_one_item(test_coll, return_dict, indx_tmp)

            if now_request['choose_mode']=='random':
                if 'seed' in now_request['choose_argu']:
                    rand_seed   = now_request['choose_argu']['seed']
                else:
                    rand_seed   = 0

                np.random.seed(rand_seed)

                for indx_tmp in np.random.choice(range(num_ava), min(now_request['choose_argu']['number'], num_ava)):
                    inc_one_item(test_coll, return_dict, indx_tmp)

            #return_dict[0]  = test_coll[0]
            #return_dict[0].pop('_id')

        print(len(return_dict))
        socket_self.send_json(return_dict)
        #socket_self.send("World from %s" % port)
