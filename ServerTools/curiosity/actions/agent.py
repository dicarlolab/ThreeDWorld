# Agent module
import action
import perception
import connection
import multiprocessing

class Agent:
    def __init__(self, action_system, visual_system, connection):
        assert isinstance(action_system, action.Action), \
                'First argument should be an Action system!'
        assert isinstance(visual_system, preception.Visual), \
                'Second argument should be a Visual system!'
        assert isinstance(connection, connection.Connection), \
                'Third argument should be a Connection!'
        self.action_system = action_system
        self.visual_system = visual_system
        self.connection = connection


    def run():
        action_loop = multiprocessing.Process(target=
