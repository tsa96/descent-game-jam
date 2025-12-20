class_name Game
extends Node


@onready var world := $World
@onready var player := $World/Player as Player
@onready var pause_menu := $Interface/PauseMenu as PauseMenu
@onready var death_screen := $Interface/DeathScreen as DeathScreen
@onready var hud := $Interface/HUD as HUD
@onready var debug_ui := $Interface/Debug
@onready var bgm := $Audio/BGMEventEmitter as FmodEventEmitter2D


# Curve resource describe how scroll speed and music change over time.
# Y pos is the scrolling speed, and music intensity increases occur at the 3 rightmost points.
var game_speed_curve := load("res://game_speed_curve.tres") as Curve
# Maximum Y position on the curve. Past this point we extrapolate.
var curve_max_x: float = 12000.0
var curve_max_y = game_speed_curve.sample(curve_max_x)
var curve_grad_extrapolated = (curve_max_y - game_speed_curve.sample(curve_max_x - 1000)) / 1000
var p1 = game_speed_curve.get_point_position(1)
var p2 = game_speed_curve.get_point_position(2)
var p3 = game_speed_curve.get_point_position(3)
var posMax = 0


func _process(_delta: float) -> void:
	var pos = maxf(posMax, player.position.y)
		
	var p = pos / curve_max_x
	if pos <= curve_max_x:
		player.scroll_speed = game_speed_curve.sample(p) * 100
	else:
		# y = mx + c type shit
		player.scroll_speed = pos * curve_grad_extrapolated + curve_max_y * 100
	
	# Note that music transitions intensities at ~0.5 and ~1. All other values are meaningless.
	if p > p3[0]:
		bgm.set_parameter("Intensity", 1.0)
	elif p > p2[0]:
		bgm.set_parameter("Intensity", 0.5)
	else:
		bgm.set_parameter("Intensity", 0.0)


func _unhandled_input(input_event: InputEvent) -> void:
	if input_event.is_action_pressed(&"toggle_fullscreen"):
		var mode := DisplayServer.window_get_mode()
		if mode == DisplayServer.WINDOW_MODE_FULLSCREEN or mode == DisplayServer.WINDOW_MODE_EXCLUSIVE_FULLSCREEN:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
		else:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
		get_tree().root.set_input_as_handled()

	elif input_event.is_action_pressed(&"toggle_pause") and not player.dead:
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
	
	elif input_event.is_action_pressed(&"toggle_devui"):
		debug_ui.visible = !debug_ui.visible
	

# Completely reset game state. Bye!
func reset() -> void:
	player.reset()
	get_tree().paused = false
	death_screen.close()
	pause_menu.close()
	hud.show()

func _on_player_on_reset() -> void:
	world.ResetWorld()

func _on_player_on_death() -> void:
	var tree := get_tree()
	tree.paused = true
	death_screen.open()
	get_tree().root.set_input_as_handled()

func _on_player_on_start_death() -> void:
	hud.hide()
