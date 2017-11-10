import uuid
import zmq
import json
import subprocess
import socket
from pymongo import MongoClient
import psutil
import os
from tabulate import tabulate
import datetime
import signal
import sys
#import fcntl
import struct

DEFAULT_QUEUE_PORT=23402

# Set your code and build directories as environment variables!
#os.environ['THREEDWORLD_BUILD_DIR'] = "C:\\Users\\mrowca\\Desktop\\world"
#os.environ['THREEDWORLD_CODE_DIR'] = "C:\\Users\\mrowca\\Documents\\GitHub\\ThreeDWorld"
#os.environ['THREEDWORLD_BUILD_DIR'] = "/data/nhaber/tdw_stuffs/linux_stuffs/locked_rot"
#os.environ['THREEDWORLD_CODE_DIR'] = "/home/nhaber/projects/ThreeDWorld"

GPU_NUM = 4


class Three_D_World_Queue(object):

        def __init__(self):
                #set defaults
                self.debug = True

                #TODO: rather hacky, but works for now
                s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                s.connect(("google.com",80))
                self.host_address = s.getsockname()[0]
                s.close()

                print("host: " + self.host_address)
                self.queue_port_number = os.environ.get('THREEDWORLD_QUEUE_PORT', DEFAULT_QUEUE_PORT)
                self.build_dir = os.environ['THREEDWORLD_BUILD_DIR']
                self.code_dir = os.environ['THREEDWORLD_CODE_DIR']

                #get commandline args
                args = sys.argv

                #TODO:  this argument parsing hackery should be replaced with argparse
                for i in range(len(args)):
                        if (args[i].startswith("--debug=")):
                                self.debug = int(args[i][8:])
                        if (args[i].startswith("--hostaddress=")):
                                self.host_address = args[i][14:]
                        if (args[i].startswith("--port=")):
                                self.queue_port_number = args[i][7:]
                        if (args[i].startswith("--builddir=")):
                                self.build_dir = args[i][11:]
                        if (args[i].startswith("--codedir=")):
                                self.code_dir = args[i][11:]

                if (not os.path.isdir(self.build_dir)):
                        raise NameError("Build path is not a directory!")
                if (not os.path.isdir(self.code_dir)):
                        raise NameError("Code path is not a directory!")


                #get networking info
                self.ctx = zmq.Context()

                print ('\n\n')
                print ('=' * 60)
                print (" " * 17, "WELCOME TO 3D WORLD QUEUE")
                print ('=' * 60)
                print ("\n")

                if (self.debug):
                        print ("\nconnecting to port...")
                self.sock1 = self.ctx.socket(zmq.REP)
                self.sock1.bind("tcp://" + self.host_address + ":" + str(self.queue_port_number))
                if (self.debug):
                        print ("...connected @%s:%d\n\n" % (self.host_address,  self.queue_port_number))

                #get mongod collection info
                if (self.debug):
                        print ("connecting to MongoD Database...")
                client = MongoClient('localhost', 23502)
                #TODO: find way to check that queue is actually connected to the database!
                if (self.debug):
                        print ("...connected\n\n")

                db = client.environment_database
                self.process_info = db.process_info

                #check and update mongod collection info with system
                self.scan_process_status()


        #main loop
        def run(self):
                while True:
                        msg = self.recv_json(self.sock1)
                        self.handle_message(msg)
       
        #select gpu
        gpu_id = 0
        def next_gpu(self):
            self.gpu_id += 1
            if self.gpu_id == GPU_NUM:
                self.gpu_id = 0
            return str(self.gpu_id)
        #sub main loop
        def handle_message(self, message):
            reactions = {"CREATE_ENVIRONMENT_1": self.create_environment__1,
                         "CREATE_ENVIRONMENT_2": self.create_environment__2,
                         "GET_ACTIVE_ENVIRONMENTS": self.get_active_processes,
                         "JOIN_ENVIRONMENT_1": self.join_environment__1,
                         "JOIN_ENVIRONMENT_2": self.join_environment__2,
                         "CHECK_PORT": self.manual_port_check,
                         "AUTO_SELECT_PORT" : self.automatic_port_selection}
            j = json.loads(message)
            try:
                if (j["msg"]["msg_type"] in reactions.keys()):
                    self.send_json(reactions[j["msg"]["msg_type"]](j), self.sock1)
                else:
                    self.send_json(json.dumps({"msg": {"msg_type": "INVALID_MSG_TYPE_RECV"}}))
            except KeyError:
                self.send_json(json.dumps({"msg": {"msg_type": "INVALID_MSG_FMT"}}))


        #################################################################################################
                                                                    #MESSAGE REACTION FUNCTIONS#
        #################################################################################################

        #send active environments
        def get_active_processes(self, j):
                return json.dumps({"msg" : {"msg_type" : "ACTIVE_PROCESSES"}, "processes" : self.get_active_environments()})


        #send available options to join to client
        def join_environment__1(self, j):
                available_environments = self.get_active_environments()

                formatted_available_environments = dict()
                for env in available_environments:
                        formatted_available_environments[env["env_owner"] + ", " +
                                                         env["port_num"] + ", " +
                                                         datetime.datetime.fromtimestamp(
                                                                 float(env["proc_create_time"])).strftime("%Y-%m-%d %H:%M:%S") + ", " +
                                                         env["env_desc"]] = env["port_num"]
                if (len(formatted_available_environments) > 0):
                        return self.send_options(formatted_available_environments.keys(), "Select an environment to join:")
                else:
                        return json.dumps({"msg" : {"msg_type" : "NO_AVAILABLE_ENVIRONMENTS"}})

        #send port number of selected environment or report that environment does not exist anymore
        def join_environment__2(self, j):
                req_keys = ["selected"]
                self.scan_for_contents(j, req_keys)

                available_environments = self.get_active_environments()

                formatted_available_environments = dict()
                for env in available_environments:
                        formatted_available_environments[env["env_owner"] + ", " +
                                                         env["port_num"] + ", " +
                                                         datetime.datetime.fromtimestamp(
                                                                 float(env["proc_create_time"])).strftime("%Y-%m-%d %H:%M:%S") + ", " +
                                                         env["env_desc"]] = env["port_num"]

                if (j["selected"] in formatted_available_environments.keys()):
                        return json.dumps({"msg" : {"msg_type" : "JOIN_OFFER"},
                                           "port_num" : formatted_available_environments[j["selected"]]})
                else:
                        return json.dumps({"msg" : {"msg_type" : "ENVIRONMENT_UNAVAILABLE"}})


        #check that port number is available then send options for builds
        def create_environment__1(self, j):
                req_keys = ["port_num"]
                self.scan_for_contents(j, req_keys)

                if (not self.check_port_num(j["port_num"])):
                    return json.dumps({"msg" : {"msg_type" : "PORT_UNAVAILABLE"}})

                #collect available builds in build_dir
                builds = list()
                file_extension = ".x86_64"
                if os.name == 'nt':
                    file_extension = ".exe"
                for root, _, files in os.walk(self.build_dir):
                    for f in files:
                        if f.endswith(file_extension):
                            builds.append(os.path.join(root, f))

                return self.send_options(builds, "Select a build:")

        #receive forward port option then parse command
        def create_environment__2(self, j):
            req_keys = ["port_num", "selected_build", "username", "description"]
            self.scan_for_contents(j, req_keys)
            if (not self.check_port_num(j["port_num"])):
                return json.dumps({"msg" : {"msg_type" : "PORT_UNAVAILABLE"}})

            #acquire a free port number for environment
            s = socket.socket()
            s.bind(('', 0))
            forward_port_num = s.getsockname()[1]
            s.close()

            print ("nohup " + j["selected_build"] +  " -force-opengl -port=" + str(forward_port_num) + " -address=" + self.host_address + " -batchmode")

            #separate j and assign defaults to optional args excluded from message
            process = ["nohup",
                       j["selected_build"],
                       "-force-opengl",
                       "-port=" + str(forward_port_num),
                       "-address=" + self.host_address,
                       #"-batchmode"
			]
           
            if os.name == 'nt':
                process = [j["selected_build"],
                           "-gpu", self.next_gpu(),
                           "-port=" + str(forward_port_num),
                           "-address=" + self.host_address]

            if ("screen_width" in j.keys()):
                process = process + ["-screenWidth=" + str(j["screen_width"])]

            if ("screen_height" in j.keys()):
                process = process + ["-screenHeight=" + str(j["screen_height"])]

            if ("profiler_frames" in j.keys()):
                process = process + ["-profilerFrames=" + str(j["profiler_frames"])]

            if ("num_time_steps" in j.keys()):
                process = process + ["-numTimeSteps=" + str(j["num_time_steps"])]

            if ("time_step_duration" in j.keys()):
                process = process + ["-timeStep=" + str(j["time_step_duration"])]

            if ("pref_img_format" in j.keys()):
                process = process + ["-preferredImageFormat=" + str(j["pref_img_format"])]

            if ("should_create_server" in j.keys() and j["should_create_server"]):
                process = process + ["-shouldCreateServer"]

            if ("should_create_test_client" in j.keys() and j["should_create_test_client"]):
                process = process + ["-shouldCreateTestClient"]

            if ("debug_net_msgs" in j.keys() and j["debug_net_msgs"]):
                process = process + ["-debugNetworkMessages"]

            if ("log_simple_time_info" in j.keys() and j["log_simple_time_info"]):
                process = process + ["-logSimpleTimeInfo"]

            if ("log_detailed_time_info" in j.keys() and j["log_detailed_time_info"]):
                process = process + ["-logDetailedTimeInfo"]
                
            if ("target_FPS" in j.keys()):
                process = process + ["-targetFPS=" + str(j["target_FPS"])]

            if ("save_debug_image_files" in j.keys()):
                process = process + ["-saveDebugImageFiles"]

            if ('gpu_num' in j.keys()):
                gpu_num = str(j['gpu_num'])
            else:
                gpu_num = '0'
            print('gpu num: ' + gpu_num)

                #start environment process
            if (self.debug):
                print ("Running Environment:")
                print (" ".join(process))
                print ("\nenvironment @%s:%d" % (self.host_address, forward_port_num))
            my_env = os.environ.copy()
            if os.name == 'nt':
                environment = subprocess.Popen(process, env=my_env)
            else:
                my_env['DISPLAY'] = ':0.' + gpu_num
                environment = subprocess.Popen(process, env=my_env, preexec_fn=self.preexec_function)
            if (self.debug):
                print ("environment pid: %d" % environment.pid)

                #start forward port process
            if (self.debug):
                print ("\n\nRunning Forward Port:")
                print ("nohup python " + os.path.join(self.code_dir, 'threedworld', 'servertools', "forward.py") +
                       " --port=" + str(j["port_num"]) +
                       " --hostaddress=" + self.host_address +
                       " --forwardport=" + str(forward_port_num) +
                       " --forwardhostaddress=" + self.host_address +
                       " --forwardpid=" + str(environment.pid))
                print ("\nforward port @%s:%d -> %s:%d" % (self.host_address,
                                                                   j["port_num"],
                                                                   self.host_address,
                                                                   forward_port_num))
            if os.name == 'nt':
                port_forwarder = subprocess.Popen(["python",
                                                    os.path.join(self.code_dir,
                                                                'threedworld',
                                                                "servertools",
                                                                "forward.py"),
                                                   "--port=" + str(j["port_num"]),
                                                   "--hostaddress=" + self.host_address,
                                                   "--forwardport=" + str(forward_port_num),
                                                   "--forwardhostaddress=" + self.host_address])
            else:
                port_forwarder = subprocess.Popen(["nohup",
                                                   "python",
                                                    os.path.join(self.code_dir,
                                                                'threedworld',
                                                                "servertools",
                                                                "forward.py"),
                                                   "--port=" + str(j["port_num"]),
                                                   "--hostaddress=" + self.host_address,
                                                   "--forwardport=" + str(forward_port_num),
                                                   "--forwardhostaddress=" + self.host_address,
                                                   "--forwardpid=" + str(environment.pid)],
                                                   preexec_fn=self.preexec_function)


            if (self.debug):
                print ("forward port pid: %d"  % port_forwarder.pid)

            #add to mongod collection
            if (self.debug):
                print ("\nAdded environment to database.\n\n")
            self.make_mongo_entry(self.make_uuid(),
                                   j["username"],
                                   j["description"],
                                   environment.pid,
                                   psutil.Process(environment.pid).create_time(),
                                   psutil.Process(port_forwarder.pid).create_time(),
                                   j["port_num"],
                                   forward_port_num,
                                   port_forwarder.pid)
             
            return self.send_join_offer(j["port_num"])

        def automatic_port_selection(self, j):
                #acquire a free port number for environment
                s = socket.socket()
                s.bind(('', 0))
                port_num = int(s.getsockname()[1])
                s.close()

                return json.dumps({"msg" : {"msg_type" : "AUTO_SELECT_PORT"}, "port_num" : port_num})

        def manual_port_check(self, j):
                is_free = self.check_port_num(j["port_num"])

                return json.dumps({"msg" : {"msg_type" : "PORT_STATUS"},
                                   "status" : is_free})

        #################################################################################################


        #################################################################################################
                                                                             #HELPER FUNCTIONS#
        #################################################################################################

        #send and receive methods
        def send_json(self, msg, sock):
                if (self.debug):
                        print (">" * 20)
                        print ("sending message...")
                sock.send_json(msg);
                if (self.debug):
                        print ("...sent message:\n\n", msg)
                        print (">" * 20, "\n\n")

        def recv_json(self, sock):
                if (self.debug):
                        print ("<" * 20)
                        print ("waiting for message...")
                msg = sock.recv_json()
                if (self.debug):
                        print ("...message received:\n\n", msg)
                        print ("<" * 20, "\n\n")
                return msg

        #Safe scan for all msg contents
        #returns True if all keys are in msg, otherwise throws a KeyError
        def scan_for_contents(self, j, req_keys):
                for key in req_keys:
                        j[key]
                return True

        def check_port_num(self, port_num):
                s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                try:
                        s.bind((self.host_address, port_num))
                except socket.error as e:
                        s.close()
                        if (e.errno == 98):
                                return False
                        else:
                                raise e
                s.close()
                return True

        def send_options(self, options, title):
                return json.dumps({"msg" : {"msg_type" : "SEND_OPTIONS"}, "options" : options, "title" : title})

        def send_join_offer(self, port_num):
                return json.dumps({"msg" : {"msg_type" : "JOIN_OFFER"}, "port_num" : port_num})

        def get_active_environments(self):
                self.scan_process_status()

                print ('_' * 60)
                print (" " * 18, "Find Active Environments")
                print ('_' * 60, "\n")

                i = 0

                if (self.debug):
                        for entry in self.process_info.find({},
                                                            {"env_uuid" : 1,
                                                             "env_owner" : 1,
                                                             "port_num" : 1,
                                                             "env_desc" : 1,
                                                             "proc_pid" : 1,
                                                             "proc_create_time" : 1,
                                                             "_id" : 0}):
                                print ("Entry", i ,"--->", entry, "\n")
                                i = i + 1

                entries = list()
                for entry in self.process_info.find({}, {"env_uuid" : 1,
                                                         "env_owner" : 1,
                                                         "port_num": 1,
                                                         "env_desc" : 1,
                                                         "proc_pid" : 1,
                                                         "proc_create_time" : 1,
                                                         "_id" : 0}):
                        entries = entries + [entry]

                print ("=" * 60)

                return entries

        #makes a mongoD entry with an environment uuid, owner, description, environment pid, environment create time, forward port number (client connects to), environment port number (server binds to), and forward pid
        def make_mongo_entry(self, env_uuid, env_owner, env_desc, proc_pid, proc_create_time, forward_create_time, forward_port, env_port, forward_pid):
                entry = {
                        "env_uuid" : env_uuid,
                        "env_owner" : env_owner,
                        "env_desc" : env_desc,
                        "proc_pid" : proc_pid,
                        "proc_create_time" : proc_create_time,
                        "forward_create_time" : forward_create_time,
                        "port_num" : forward_port, #confusing to look at, but this the port number of the forward address which the client connects to
                        "env_port_num" : env_port, #this on the other hand is what the forward port forwards messages from port_num to
                        "forward_pid" : forward_pid
                        }
                self.process_info.insert_one(entry)

        def make_uuid(self):
                return uuid.uuid1()

        def print_processes(self, entries):
                table = list()
                for entry in entries:
                        table = table + [[entry["env_owner"],
                                          entry["proc_pid"],
                                          entry["port_num"],
                                          datetime.datetime.fromtimestamp(
                                                  float(entry["proc_create_time"])).strftime("%Y-%m-%d %H:%M:%S"),
                                          entry["env_desc"]]]

                print (tabulate(table, headers=["Owner", "PID", "Port", "Create Time", "Description"], tablefmt="fancy_grid"))

        def preexec_function(self):
                # Ignore the SIGINT signal by setting the handler to the standard signal handler SIG_IGN.
                signal.signal(signal.SIGINT, signal.SIG_IGN)

        def scan_process_status(self):
                if (self.debug):
                        print ('_' * 60)
                        print (" " * 18,"Updating MongoD database")
                        print ('_' * 60)

                        print ("\nBefore Scan:")
                        entries = list()
                        for entry in self.process_info.find():
                                entries = entries + [entry]
                        self.print_processes(entries)

                #retrieve mongod records
                records = list()
                for entry in self.process_info.find():
                        records = records + [entry]

                #update mongoD database and create new forward ports
                for pid in psutil.pids():
                        proc = psutil.Process(pid)
                        try:
                            proc_path = proc.exe()
                        except psutil.AccessDenied:
                            print ("'%s' Process is not allowing us to view exe!" % proc.name)
                            continue
                        if (proc_path.startswith(self.build_dir)):
                                #check if process is already in the database
                                for entry in self.process_info.find({"proc_pid" : pid,
                                                                     "proc_create_time" : proc.create_time()}):
                                        if entry in records:
                                                records.remove(entry)

                                #else process is undocumented and will be added to the database
                                if (self.process_info.find({"proc_pid" : pid,
                                                            "proc_create_time" : proc.create_time()}).count() == 0):
                                        #acquire a free port number for environment
                                        s = socket.socket()
                                        s.bind(('', 0))
                                        forward_port_num = s.getsockname()[1]
                                        s.close()

                                        #start new forward port process
                                        if (len(proc.connections(kind="tcp")) > 0):
                                            if (self.debug):
                                                print ("\nNEW PORT:")
                                                print ("\nforward port @%s:%d -> %s:%d" % (self.host_address,
                                                                                           forward_port_num,
                                                                                           proc.connections(kind="tcp")[0].laddr[0],
                                                                                           proc.connections(kind="tcp")[0].laddr[1]))
                                            if os.name == 'nt':    
                                                port_forwarder = subprocess.Popen(["python",
                                                                                   os.path.join(self.code_dir,
                                                                                                "threedworld",
                                                                                                "servertools",
                                                                                                "forward.py"),
                                                                                   "--port=" + str(forward_port_num),
                                                                                   "--hostaddress=" + self.host_address,
                                                                                   "--forwardport=" + str(proc.connections(kind="tcp")[0].laddr[1]),
                                                                                   "--forwardhostaddress=" + proc.connections(kind="tcp")[0].laddr[0]])
                                            else:
                                                port_forwarder = subprocess.Popen(["nohup",
                                                                                   "python",
                                                                                   os.path.join(self.code_dir,
                                                                                                "threedworld",
                                                                                                "servertools",
                                                                                                "forward.py"),
                                                                                   "--port=" + str(forward_port_num),
                                                                                   "--hostaddress=" + self.host_address,
                                                                                   "--forwardport=" + str(proc.connections(kind="tcp")[0].laddr[1]),
                                                                                   "--forwardhostaddress=" + proc.connections(kind="tcp")[0].laddr[0]],
                                                                                   "--forwardpid=" + pid,
                                                                                   preexec_fn=self.preexec_function)

                                            if (self.debug):
                                                print ("forward port pid: " + str(port_forwarder.pid))

                                        self.make_mongo_entry(self.make_uuid(),
                                                              "Undocumented",
                                                              "Undocumented",
                                                              pid,
                                                              proc.create_time(),
                                                              psutil.Process(port_forwarder.pid).create_time(),
                                                              forward_port_num,
                                                              proc.connections(kind="tcp")[0].laddr[1],
                                                              port_forwarder.pid)

                        #TODO: make way to ping environment for more information instead of leaving things undocumented

                #remove processes no longer running from mongod database
                for entry in records:
                        self.process_info.delete_many(entry)

                if (self.debug):
                        print ("\nAfter Scan:")
                        entries = list()
                        for entry in self.process_info.find():
                                entries = entries + [entry]
                        self.print_processes(entries)
                        print ('=' * 60)
                        print ("\n")

        #################################################################################################

#script segment
if __name__ == '__main__':
    queue = Three_D_World_Queue()
    queue.run()
