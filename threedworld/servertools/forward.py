#!/usr/bin/env python

import sys
import zmq
import os
import signal
import time
import socket
import multiprocessing
import json

args = sys.argv

debug = True
port_num=0
host_address=""
forward_port_num=0
forward_host_address=""
forward_pid=0
# timeout time in seconds; defaults to 6 hours
timeout = 3600 * 6

for i in range(len(args)):
        if (args[i].startswith("--port=")):
                port_num = int(args[i][7:])
        if (args[i].startswith("--hostaddress=")):
                host_address = args[i][14:]
        if (args[i].startswith("--forwardport=")):
                forward_port_num = int(args[i][14:])
        if (args[i].startswith("--forwardhostaddress=")):
                forward_host_address = args[i][21:]
        if (args[i].startswith("--forwardpid=")):
                forward_pid = int(args[i][13:])

ctx = zmq.Context()

def check_port_num(port_num):
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        try:
                print("poking environment at port %d" % int(port_num))
                s.bind((host_address, int(port_num)))
        except socket.error as e:
                s.close()
                print("error number %d" % e.errno)
                if (e.errno in [98, 10048]):
                        print("is active")
                        return False
                else:
                        raise e
        s.close()
        print("not active")
        return True

def check_if_environment_up():
        inactives_count = 0
        while True:
                print("sleep time")
                time.sleep(5)
                print("checking if active...")
                if (check_port_num(forward_port_num)):
                        inactives_count += 1
                else:
                        inactives_count = 0
                if (inactives_count > 60):
                        sys.exit()
                print("...completed check, inactive for %d time steps" % inactives_count)

def run(forward_pid, timeout):
        sock1 = ctx.socket(zmq.REP)
        sock2 = ctx.socket(zmq.REQ)
        # Poller for timeouts:
        sock1.setsockopt(zmq.LINGER, 0)
        poller = zmq.Poller()
        poller.register(sock1, zmq.POLLIN)

        print ("FORWARD PORT binding at: " + "tcp://" + host_address + ":" + str(port_num))
        sock1.bind("tcp://" + host_address + ":" + str(port_num))
        print ("FORWARD PORT connecting to: " + "tcp://" + forward_host_address + ":" + str(forward_port_num)), "\n"
        sock2.connect("tcp://" + forward_host_address + ":" + str(forward_port_num))
        
        while (True):
                if (debug):
                        print("waiting for message from client...")
                if poller.poll(timeout * 1000): # timeout in seconds
                    msg = sock1.recv_json()
                else:
                    if forward_host_address == host_address and forward_pid > 0:
                        print("Timeout reached: Killing environment with pid %d" % forward_pid)
                        try:
                            os.kill(forward_pid, signal.SIGKILL)
                        except:
                            print('ERROR Could not kill environment with pid %d. Try sudo?' \
                                    % forward_pid)
                        sys.exit()
                n = msg['n']
                if 'client_timeout' in msg:
                    timeout = msg.pop('client_timeout')
                if (debug):
                        print("...received message from client\n")
                        print("sending message to server...")
                sock2.send_json(msg["msg"])
                if (debug):
                        print("...sent message to server\n")
                        print("waiting for message from server")
                msg = [sock2.recv() for _ in range(n)]
                if (debug):
                        print("...received message from server\n")
                        print("sending message to client...")
                # fast way to insert the "environment_pid" field into the string
                # without jsonizing it
                msg[0] = "{\"environment_pid\": " + str(forward_pid) + "," + msg[0][1:]
                sock1.send_multipart(msg)
                if (debug):
                        print("...sent message to client\n")


if __name__ == '__main__':
    t1 = multiprocessing.Process (target=run, args=(forward_pid,timeout,))
    t2 = multiprocessing.Process (target=check_if_environment_up)

    t1.start()
    t2.start()

    while (True):
        time.sleep(3)
        if (not t2.is_alive()):
                t1.terminate()
                try:
                    os.kill(forward_pid, signal.SIGKILL)
                except:
                    print('ERROR Could not kill environment with pid %d. Try sudo?' \
                            % forward_pid)
                sys.exit()
        if (not t1.is_alive()):
                t2.terminate()
                try:
                    os.kill(forward_pid, signal.SIGKILL)
                except:
                    print('ERROR Could not kill environment with pid %d. Try sudo?' \
                            % forward_pid)
                os.kill(forward_pid, signal.SIGKILL)
                sys.exit()
