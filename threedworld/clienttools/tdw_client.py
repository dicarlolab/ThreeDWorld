import os, json, datetime
import zmq
from tabulate import tabulate
from pick import pick

from threedworld.servertools.tdw_queue import DEFAULT_QUEUE_PORT

class TDW_Client(object):

    def __init__(self, host_address,
                 queue_port_num=DEFAULT_QUEUE_PORT,
                 requested_port_num=None,
                 auto_select_port=True,
                 environment_config=None,
                 debug=True,
                 selected_build=None,
                 selected_forward=None,
                 initial_command="",
                 username=None,
                 description=None,
                 num_frames_per_msg=4,
                 get_obj_data=False,
                 send_scene_info=False,
                 environment_profile=None
                 ):

        """
        :Args:
            host_address (str)

        :Kwargs:
            - queue_port_num (str, default: 23402)
                The port number to bind to the queue. Unless you know for certain the queue is running on a different port, leave this one alone.

            - environment_config (dict, default: {'environment_scene': 'Empty'})
                The environment configuration. Must have at least the type of the scene mentioned as seen in the default dictionary. (See the methods section below for changing the client config file post initialization if you say wanted to create a new environment for the client with a different config, or wanted to run a reset scene with a different config).

            - debug (bool, default=False)
                When true will give you important network and message info.

            - selected_build (str, default: '')
                When left blank, you will be required to select a build from the available builds on the server using a menu. If you want to skip this manual step, just set this to the name of the binary file `'<build_name>.x86_64'`

            - initial_command (str, default: '')
                When left blank or invalid, you will be required to select available commands from a menu. Options:
                    - 'request_create_environment'
                    - 'request_join_environment'
                    - 'request_active_environments'
                Whichever command you type, the client will start by running this command.

            - requested_port_num (int, default: None)
                When left blank or invalid, you will be required to type in a port number in the UI, or request to randomly select an available port.

            - username (str, default: '')
                If left blank, will prompt at UI.

            - description (str, default: '')
                If left blank, will prompt at UI.

            - num_frames_per_msg (int, default: 4)
                A number greater than 1 that equals the number of frames you expect back.

            - get_obj_data (bool, default: False)
                Determines whether you want object data or not. Object data is returned as a list.

            - send_scene_info (bool, default: False)
                Determines whether or not to send scene info, icluding objects in the scene
        """

        # initialize attributes
        self.queue_host_address = host_address
        self.queue_port_number = queue_port_num
        self.port_num = requested_port_num
        self.selected_build = selected_build
        self.selected_forward = selected_forward
        self.environment_config = environment_config
        self.initial_command = initial_command
        if username is None:
            username = os.environ['USER']
        self.username = username
        self.description = description
        self.num_frames_per_msg = num_frames_per_msg
        self.get_obj_data = get_obj_data
        self.send_scene_info = send_scene_info
        self.debug = debug
        self.environment_profile = environment_profile

        self.ctx = zmq.Context()

        print "\n\n"
        print '=' * 60
        print " " * 17, "WELCOME TO 3D WORLD CLIENT"
        print '=' * 60
        print "\n"

        # connect to queue at requested server
        if (self.debug):
            print ("\nconnecting...")
        self.sock = self.ctx.socket(zmq.REQ)
        self.sock.connect("tcp://" + self.queue_host_address + ":" + str(self.queue_port_number))
        if (self.debug):
            print ("...connected @%s:%d\n\n" % (self.queue_host_address, self.queue_port_number))
        # set program states
        self.connected_to_queue = True
        self.manually_pick_port_num = not auto_select_port
        self.ready_for_input = True
        self.ready_for_recv = False

    def run(self):
        """
        Main loop, returns socket connected to online or initializing environment.
        """
        commands = {
            "request_create_environment": self.request_create_environment,
            "request_active_processes": self.request_active_processes,
            "request_join_environment": self.request_join_environment,
        }

        # run initial command if specified
        if (self.initial_command in commands.keys() and not self.ready_for_recv):
            commands[self.initial_command]()

        # run commands until waiting for a message
        while(not self.ready_for_recv):
            title = "Pick a command:"
            options = commands.keys()
            option, index = pick(options, title)
            commands[option]()

        # loop while still connected to the queue
        while(self.connected_to_queue):
            # if waiting for a message, receive a message
            if (self.ready_for_recv):
                msg = self.recv_json(self.sock)

                j = json.loads(msg)

                self.ready_for_recv = False

            # run commands until waiting for a message
            while(not self.ready_for_recv):
                title = "Pick a command:"
                options = commands.keys()
                option, index = pick(options, title)
                commands[option]()

        print "=" * 60
        print " " * 19, "Client Setup Complete"
        print "=" * 60
        return self.sock

    ############################################################################
    #                               USER FUNCTIONS                             #
    ############################################################################

    def load_config(self, config_dict):
        """
        Sets environment config attribute
        """
        self.environment_config = config_dict

    def load_profile(self, profile_dict):
        self.environment_profile = profile_dict

    def reconnect(self):
        """
        Attempts to reconnect to saved port number

        If succeeds returns True else False
        """
        try:
            self.connect_to_port(self.port_num, use_config=False)
            return True
        except:
            return False

    def killall(self, username):
        msg = json.dumps({"msg": {"msg_type": "GET_ACTIVE_ENVIRONMENTS"}})
        self.send_json(msg, self.sock)
        msg = self.recv_json(self.sock)
        msg = json.loads(msg)

        if msg["msg"]["msg_type"] == "ACTIVE_PROCESSES":
            pass
        else:
            print "Error: " + msg["msg"]["msg_type"] + "\n"
            self.press_enter_to_continue()
            return

        print 'The following processes will be killed:'
        print
        killproc = [p for p in msg['processes'] if p['env_owner'] == username]
        self.print_processes(killproc)

        for proc in killproc:
            self.connect_to_port(proc['port_num'], use_config=False)
            self.send_json({'n': 0, 'msg': {'msg_type': 'TERMINATE'}})

    ############################################################################
    #                            COMMANDS TO QUEUE                             #
    ############################################################################

    def request_create_environment(self):
        """
        Requests to make an environment
        """
        print '_' * 60
        print " " * 16, "Requesting Create Environment"
        print '_' * 60, "\n"

        # select a port number if not already specified
        if (not self.port_num):
            self.pick_new_port_num()

        # phase 1
        # loop until open port number is selected
        has_valid_port_num = False
        while (not has_valid_port_num):
            msg = json.dumps({"msg": {"msg_type": "CREATE_ENVIRONMENT_1"},
                             "port_num": self.port_num})
            self.send_json(msg, self.sock)
            msg = self.recv_json(self.sock)

            msg = json.loads(msg)

            if (msg["msg"]["msg_type"] == "PORT_UNAVAILABLE"):
                self.pick_new_port_num()
            elif (msg["msg"]["msg_type"] == "SEND_OPTIONS"):
                has_valid_port_num = True
            else:
                print "Error: " + msg["msg"]["msg_type"] + "\n"
                self.press_enter_to_continue()
                return

        # reformat options
        refmt_options = []
        for option in msg['options']:
            i = option.rfind('/')
            if i < 0:
                i = 0
            refmt_options += [option[i+1:]]

        # pick option
        build_option = self.pick_option({'title': msg['title'],
                                         'options': sorted(refmt_options)},
                                        default_choice=self.selected_build)

        for opt in msg['options']:
            if opt.endswith(build_option):
                build_option = opt

        # phase 2
        username, description = self.username, self.description

        # collect username and description if not given in initialization
        while (not username):
            print "\nPlease type a username:"
            username = raw_input()
            print ""
        while (not description):
            print "\nPlease type a description:"
            description = raw_input()
            print ""

        # loop until has open port number on server
        has_valid_port_num = False
        while (not has_valid_port_num):
            base_msg = {"msg": {"msg_type": "CREATE_ENVIRONMENT_2"},
                        "port_num": self.port_num,
                        "selected_build": build_option,
                        "username": username,
                        "description": description}
            msg = base_msg.copy()

            # add config
            if (self.environment_profile):
                msg.update(self.environment_profile)

            # request environment
            msg = json.dumps(msg)
            self.send_json(msg, self.sock)

            # receive environment port number
            msg = self.recv_json(self.sock)

            msg = json.loads(msg)

            if (msg["msg"]["msg_type"] == "PORT_UNAVAILABLE"):
                self.pick_new_port_num()
            elif (msg["msg"]["msg_type"] == "JOIN_OFFER"):
                has_valid_port_num = True
            else:
                print "Error: " + msg["msg"]["msg_type"] + "\n"
                self.press_enter_to_continue()
                return

        # connect at received port
        self.port_num = msg["port_num"]
        self.connect_to_port(msg["port_num"])

        self.ready_for_recv = True

        print "=" * 60

    def request_join_environment(self):
        """
        Requests to join active environment process
        """
        print '_' * 60
        print " " * 16, "Requesting Join Environment"
        print '_' * 60, "\n"

        # phase 1
        # send join request
        msg = json.dumps({"msg": {"msg_type": "JOIN_ENVIRONMENT_1"}})
        self.send_json(msg, self.sock)

        # wait for environment options
        msg = self.recv_json(self.sock)

        msg = json.loads(msg)

        if (msg["msg"]["msg_type"] == "NO_AVAILABLE_ENVIRONMENTS"):
            print "No available environments on server!"
            self.press_enter_to_continue()
            return
        elif (msg["msg"]["msg_type"] == "SEND_OPTIONS"):
            has_valid_port_num = True
        else:
            print "Error: " + msg["msg"]["msg_type"] + "\n"
            self.press_enter_to_continue()
            return

        # pick option
        option = self.pick_option(msg, self.selected_build)

        # phase 2
        # send selected option
        msg = json.dumps({"msg": {"msg_type": "JOIN_ENVIRONMENT_2"},
                          "selected": option})
        self.send_json(msg, self.sock)

        # wait for selected options port number
        # (and eventually also confimation that selected option is still online)
        msg = self.recv_json(self.sock)

        msg = json.loads(msg)

        # handle if environment goes offline after picking environment
        if (msg["msg"]["msg_type"] == "ENVIRONMENT_UNAVAILABLE"):
            print "Environment no longer available! Look for a new environment? (y/n)"
            while True:
                ans = raw_input()
                if (ans in ["y", "Y"]):
                    self.request_join_environment()
                    return
                elif (ans in ["n", "N"]):
                    return
                else:
                    print "Not a valid response please enter \'y\' or \'n\'"
        elif (msg["msg"]["msg_type"] == "JOIN_OFFER"):
            pass
        else:
            print "Error: " + msg["msg"]["msg_type"] + "\n"
            self.press_enter_to_continue()
            return

        # connect to received port number
        self.port_num = msg["port_num"]
        self.connect_to_port(msg["port_num"], use_config=False)

        self.ready_for_recv = True

        print "=" * 60, "\n"

    def request_active_processes(self):
        """
        Request to display the relevant info for the environments on the server
        """
        print '_' * 60
        print " " * 16, "Requesting Active Processes"
        print '_' * 60
        print ""

        msg = json.dumps({"msg": {"msg_type": "GET_ACTIVE_ENVIRONMENTS"}})
        self.send_json(msg, self.sock)

        msg = self.recv_json(self.sock)

        msg = json.loads(msg)

        if (msg["msg"]["msg_type"] == "ACTIVE_PROCESSES"):
            pass
        else:
            print "Error: " + msg["msg"]["msg_type"] + "\n"
            self.press_enter_to_continue()
            return

        self.print_processes(msg["processes"])

        self.press_enter_to_continue()

    ############################################################################
    #                             HELPER FUNCTIONS                             #
    ############################################################################

    def send_json(self, msg, sock):
        """
        Send and receive functions
        """
        if (self.debug):
            print "\n", ">" * 20
            print "sending message..."
        sock.send_json(msg)
        if (self.debug):
            print "...message sent:\n", msg
            print ">" * 20

    def recv_json(self, sock):
        if (self.debug):
            print "\n", "<" * 20
            print "waiting for message..."
        msg = sock.recv_json()
        if (self.debug):
            print "...message received:\n", msg
            print "<" * 20
        return msg

    def pick_new_port_num(self):
        """"
        Split function that assigns picking to auto or manual via state
        """
        if (self.manually_pick_port_num):
            self.manual_port_selection()
        else:
            self.automatic_port_selection()

    def manual_port_selection(self):
        """
        Type a port number until one is available or you request to switch to auto
        """
        print
        print("Please enter a port number or type 'scan' or hit enter with no "       "content to sweep the host to find an available port and "
              "connect:")
        get_port_num = True
        x = None
        while (get_port_num):
            x = raw_input()
            get_port_num = False
            try:
                if (x == "scan" or len(x) == 0):
                    self.manually_pick_port_num = False
                    self.pick_new_port_num()
                    return
                x = int(x)
                if (x < 0 or x > 65535):
                    raise ValueError()
            except ValueError:
                get_port_num = True
                print ("Not a valid port number, enter a number between 0 and 65535:")

        self.send_json(json.dumps({"msg": {"msg_type": "CHECK_PORT"},
                                   "port_num": x}), self.sock)

        msg = self.recv_json(self.sock)

        msg = json.loads(msg)

        if (not msg["status"]):
            self.pick_new_port_num()
        else:
            self.port_num = x

    def automatic_port_selection(self):
        """
        Requests a free port from the server
        """
        self.send_json(json.dumps({"msg": {"msg_type": "AUTO_SELECT_PORT"}}),
                       self.sock)

        msg = self.recv_json(self.sock)

        msg = json.loads(msg)

        if (msg["msg"]["msg_type"] == "AUTO_SELECT_PORT"):
            self.port_num = msg["port_num"]
        else:
            print "Error: " + msg["msg"]["msg_type"] + "\n"
            self.press_enter_to_continue()
            return

    def press_enter_to_continue(self):
        """
        Displays a bar asking to hit enter to continue, and stalls program until this action is performed
        """
        print '=' * 60
        print " " * 18, "Press Enter to continue"
        print '=' * 60

        raw_input()

    def print_processes(self, entries):
        """
        Prints process info in a table
        """
        table = list()
        for entry in entries:
            table = table + [[entry["env_owner"],
                              entry["proc_pid"],
                              entry["port_num"],
                              datetime.datetime.fromtimestamp(float(
                                    entry["proc_create_time"])).strftime("%Y-%m-%d %H:%M:%S"),
                              entry["env_desc"]]]

        print tabulate(table, headers=["Owner", "PID", "Port", "Create Time", "Description"], tablefmt="fancy_grid")

    def pick_option(self, msg, default_choice=None):
        """
        Select from options in a menu via cursor
        """
        title = msg["title"]
        options = msg["options"]

        if default_choice is not None and default_choice in options:
            return default_choice

        option, index = pick(options, title)

        return option

    def connect_to_port(self, port_num, use_config=True):
        """
        Attempt to connect to a port, if using a config, 
        sends config with join message
        """
        self.sock.disconnect("tcp://" + self.queue_host_address + ":" + str(self.queue_port_number))

        self.connected_to_queue = False

        if (self.debug):
            print("\nconnecting...")
        self.sock.connect("tcp://" + self.queue_host_address + ":" + str(port_num))
        if (self.debug):
            print("...connected @%s:%d\n\n" % (self.queue_host_address, port_num))

        self.port_num = port_num

        if (use_config and self.environment_config):
            if (self.debug):
                print "sending with config..."
            self.sock.send_json({"n": self.num_frames_per_msg,
                                 "msg": {"msg_type": "CLIENT_JOIN_WITH_CONFIG",
                                         "config": self.environment_config,
                                         "send_scene_info":
                                            self.send_scene_info,
                                         "get_obj_data": self.get_obj_data}})
            if (self.debug):
                print "...sent with config\n"
        else:
            if (self.debug):
                print "sending without config..."
            self.sock.send_json({"n": self.num_frames_per_msg,
                                 "msg": {"msg_type": "CLIENT_JOIN",
                                         "send_scene_info":
                                             self.send_scene_info,
                                         "get_obj_data": self.get_obj_data}})
            if (self.debug):
                print "...sent without config\n"
