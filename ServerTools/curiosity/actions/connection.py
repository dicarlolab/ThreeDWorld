# Module connection
import zmq
import socket

# look up host address
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.connect(("google.com",80))
host_address = s.getsockname()[0]
s.close()

# define port
port = 5556

class Connection:
    def  __init__(self):        
        self.host_address = host_address
        self.port = port
        self.ctx = zmq.Context()

    def set_host(self, host_address):
        self.host_address = host_address

    def set_port(self, port):
        self.port = port
    
    def connect(self):
        print "connecting..."
        sock = ctx.socket(zmq.REQ)
        sock.connect("tcp://" + self.host_address + ":" + str(self.port))
        print "... connected @" + host_address + ":" + str(self.port)
