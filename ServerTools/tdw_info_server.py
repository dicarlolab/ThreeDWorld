import uuid
import zmq
import json
import subprocess
import socket
from pymongo import MongoClient
#import psutil
import os
from tabulate import tabulate
import datetime
import signal
import sys
import fcntl
import struct
from optparse import OptionParser
import time

if __name__ == "__main__":

    parser = OptionParser()
    parser.add_option("-p", "--port", dest="portn", default =5555, type=int)

    (options, args) = parser.parse_args()

    port = str(options.portn)
    context = zmq.Context()
    socket_self = context.socket(zmq.REP)

    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.connect(("google.com",80))

    host_address = s.getsockname()[0]

    s.close()

    print "host: " + host_address + " with port: " + port
    socket_self.bind("tcp://%s:%s" % (host_address, port))

    while True:
        #  Wait for next request from client
        print "Waiting for info: "
        message = socket_self.recv_json()
        print "Received request: ", message
        #time.sleep (1)  
        time.sleep(0.1)
        test_dict   = {"test": 0}
        socket_self.send_json(test_dict)
        #socket_self.send("World from %s" % port)


