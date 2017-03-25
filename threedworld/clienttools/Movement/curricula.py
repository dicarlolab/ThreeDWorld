'''Some curricula for ya'''

import actions.curious2 as curious2 # import make_new_batch

import numpy as np
#0: 3, 6, 50 - 130
#1: 3-5, 50 - 130
#2: 3-5, 50 - 100
simple_push = [
	('SINGLE_OBJECT', 
		'PUSHING_0', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 5), 'f_horiz_range' : range(50, 100), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		}
		)
]

simple_push_longer = [
	('SINGLE_OBJECT', 'LONG_PUSH', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(8, 10), 'f_horiz_range' : range(50, 70), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		}),
]

simple_push_shorter = [
	('SINGLE_OBJECT', 'FAST_PUSH', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(1, 2), 'f_horiz_range' : range(100, 400) + range(100, 200), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		}),
]


#0 3-5, 20 - 60
#1 3-6, 25 - 60
#2 3-7

#maybe some more basic parabolic motion -- another category that's supposed to be more

simple_lift = [
	('SINGLE_OBJECT', 'LONG_LIFT', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 7), 'f_horiz_range' : range(10), 'f_y_range' : range(25, 60), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}})
]

#0 range 1-3
#1 range 1-2, 
#2 f_horiz 0-30,
#3 f_horiz 0-60m f_y 130 - 200

lift_short_fast = [
	('SINGLE_OBJECT', 'FAST_LIFT', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}})
]

#just 0

simple_rot = [
	('SINGLE_OBJECT', 
		'ROT', 
		{
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(15, 20), 'tor_y_range' : range(100, 300), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		})
]

#0: 3-6
#1: 3-5
#2: 3-10, f_horiz 30-50
#3: 3-10, f_horiz 40-70


#faster rotation (or rotation  is fine), slower push

push_rot = [
	('SINGLE_OBJECT', 'PUSH_ROT', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 10), 'f_horiz_range' : range(40, 70), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		})
]

push_rot_slower = [
	('SINGLE_OBJECT', 'PUSH_ROT', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 10), 'f_horiz_range' : range(50, 60), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		})
]

#some more parabolic projectile structure

lift_push_rot = [
	('SINGLE_OBJECT', 'LONG_LIFT_PUSH_ROT', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 7), 'f_horiz_range' : range(5, 10), 'f_y_range' : range(25, 60), 'tor_horiz_range' : range(10, 40), 'tor_y_range' : range(50, 150), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}})
]

lift_push_rot_parabolic = [
	('SINGLE_OBJECT', 'FAST_LIFT_PUSH_ROT', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200), 'tor_horiz_range' : range(40, 80), 'tor_y_range' : range(50, 150), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}})
]


tab_push_long = [
	('PUSH_OFF_TABLE', 'LONG_PUSH', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(8, 10), 'f_horiz_range' : range(50, 70)},
		'cut_if_off_screen' : 4
		}),
]

tab_push_short = [
	('PUSH_OFF_TABLE', 'FAST_PUSH', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(100, 400) + range(100, 200)},
		'cut_if_off_screen' : 4
		}),
]


tab_lift = [
	('PUSH_OFF_TABLE', 'FAST_LIFT', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		 'random_init_rot' : True, 
		 'func' : curious2.make_constant_random_action_sequence, 
		 'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200)}})
]

tab_rot = [
	('PUSH_OFF_TABLE', 'ROT', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(15, 20), 'tor_y_range' : range(100, 300)},
		'cut_if_off_screen' : 4
		})
]


tab_push_rot = [
	('PUSH_OFF_TABLE', 'PUSH_ROT', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(3, 10), 'f_horiz_range' : range(50, 60), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150)},
		'cut_if_off_screen' : 4
		})
]

tab_lift_push_rot = [
	('PUSH_OFF_TABLE', 'FAST_LIFT_PUSH_ROT', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200), 'tor_horiz_range' : range(40, 80), 'tor_y_range' : range(50, 150)}
		})
]


tab_push_noshake = [
	('PUSH_OFF_TABLE', 'PUSH_NOSHAKE', 
		{'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(5, 10), 'f_horiz_range' : range(50, 70), 'std_dev_ang' : np.pi / 6.},
		'cut_if_off_screen' : 4
		}),
]

tab_push_down_noshake = [
	('PUSH_OFF_TABLE', 'PUSH_DOWN_NOSHAKE', 
		{
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(5, 10), 'f_horiz_range' : range(50, 70), 'f_y_range' : range(-100, -50), 'std_dev_ang' : np.pi / 6.},
		'cut_if_off_screen' : 4
		}),
]


tab_lift_noshake = [
	('PUSH_OFF_TABLE', 'FAST_LIFT_NOSHAKE', 
		{
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		 'random_init_rot' : True, 
		 'func' : curious2.make_constant_random_action_sequence, 
		 'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200)}})
]

tab_rot_noshake = [
	('PUSH_OFF_TABLE', 'ROT_NOSHAKE', 
		{
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(15, 20), 'tor_y_range' : range(100, 300)},
		'cut_if_off_screen' : 4
		})
]

tab_push_rot_noshake = [
	('PUSH_OFF_TABLE', 'PUSH_ROT_NOSHAKE', 
		{ 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(3, 10), 'f_horiz_range' : range(50, 60), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150)},
		'cut_if_off_screen' : 4
		})
]

tab_lift_push_rot_noshake = [
	('PUSH_OFF_TABLE', 'FAST_LIFT_PUSH_ROT_NOSHAKE', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200), 'tor_horiz_range' : range(40, 80), 'tor_y_range' : range(50, 150)}
		})
]


other_obj_curriculum = [tab_push_noshake, tab_push_down_noshake, tab_lift_noshake, tab_push_rot_noshake, tab_lift_push_rot_noshake]













new_curriculum = [simple_push_longer, simple_push_shorter, simple_lift, lift_short_fast, simple_rot, push_rot_slower, lift_push_rot, lift_push_rot_parabolic]

#1: y-height  = 2 * table y
#2: same, + random initial rotation


new_table_curriculum = [tab_push_long, tab_push_short, tab_lift, tab_rot, tab_push_rot, tab_lift_push_rot]




#np.pi / 6.
#controlled table task


controlled_table_push_long = [
	('CONTROLLED_TABLE_TASK',
		'LONG_PUSH',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(8, 10), 'f_horiz_range' : range(50, 70)},
		'cut_if_off_screen' : 4
		}

		)
]


controlled_table_push = [
	('CONTROLLED_TABLE_TASK',
		'FAST_PUSH',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(100, 400) + range(100, 200)},
		'cut_if_off_screen' : 4
		}

		)
]


controlled_table_push_lowvar = [
	('CONTROLLED_TABLE_TASK',
		'PUSH_LOWVAR',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : .001, 'time_len_range' : range(1, 2), 'f_horiz_range' : range(100, 400) + range(100, 200)},
		'cut_if_off_screen' : 4
		}

		)
]


controlled_table_lift = [
	('CONTROLLED_TABLE_TASK',
		'LIFT',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200)},
		}
		)
]

controlled_table_rot = [
	('CONTROLLED_TABLE_TASK',
		'ROT',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(15, 20), 'tor_y_range' : range(100, 300)},
		'cut_if_off_screen' : 4
		}
		)
]

controlled_table_push_rot = [
	('CONTROLLED_TABLE_TASK',
		'PUSH_ROT',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(3, 10), 'f_horiz_range' : range(50, 60), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150)},
		'cut_if_off_screen' : 4		
		}
		)
]

controlled_table_lift_push_rot = [
	('CONTROLLED_TABLE_TASK',
		'FAST_LIFT_PUSH_ROT',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200), 'tor_horiz_range' : range(40, 80), 'tor_y_range' : range(50, 150)},
		}

		)
]

controlled_table_curriculum = [controlled_table_push_long, controlled_table_push, controlled_table_lift, controlled_table_rot, controlled_table_push_rot, controlled_table_lift_push_rot]

controlled_table_simple_test = [controlled_table_push_lowvar]

wall_throw = [
	('WALL_THROW',
		'THROW_BEHIND',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(130, 300), 'f_y_range' : range(130, 200)}
		}


		)

]

object_throw = [
	('COLLISION',
		'THROW_AT_OBJECT',
		{
		'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 100., 'time_len_range' : range(2, 3), 'f_horiz_range' : range(130, 200), 'f_y_range' : range(30, 100)}
		}


		)
]

wall_throw_curriculum = [wall_throw]

object_throw_curriculum = [object_throw]

