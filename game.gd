class_name Game
extends Node

@onready var world := $World
@onready var player := $World/Player as CharacterBody2D
@onready var pause_menu := $Interface/PauseMenu as PauseMenu

# Completely reset game state. Bye!
func reset() -> void:
	$World.ResetWorld()
	player.position = Vector2(0, 0)

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
