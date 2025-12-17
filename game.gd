class_name Game
extends Node

@onready var world := $World
@onready var player := $World/Player as Player
@onready var pause_menu := $Interface/PauseMenu as PauseMenu

@onready var bgm := $Audio/BGMEventEmitter as FmodEventEmitter2D
@export var full_intensity_pos_threshold := 50000.0 as float


# Completely reset game state. Bye!
func reset() -> void:
	world.ResetWorld()
	player.reset()


func _unhandled_input(input_event: InputEvent) -> void:
	if input_event.is_action_pressed(&"toggle_fullscreen"):
		var mode := DisplayServer.window_get_mode()
		if mode == DisplayServer.WINDOW_MODE_FULLSCREEN or mode == DisplayServer.WINDOW_MODE_EXCLUSIVE_FULLSCREEN:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
		else:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
		get_tree().root.set_input_as_handled()

	elif input_event.is_action_pressed(&"toggle_pause"):
		var tree := get_tree()
		tree.paused = not tree.paused
		if tree.paused:
			pause_menu.open()
		else:
			pause_menu.close()
		get_tree().root.set_input_as_handled()

	elif input_event.is_action_pressed(&"reset"):
		reset()
		get_tree().root.set_input_as_handled()

func _process(_delta: float) -> void:
	# Note that music transitions intensities at ~0.33 and ~0.66
	bgm.set_parameter("Intensity", (player.position.y / full_intensity_pos_threshold) * (2.0 / 3.0))
