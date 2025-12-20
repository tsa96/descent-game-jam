class_name Game
extends Node

@onready var world := $World
@onready var player := $World/Player as Player
@onready var beast := $World/Sticky/Beast as Beast
@onready var pause_menu := $Interface/PauseMenu as PauseMenu
@onready var death_screen := $Interface/DeathScreen as DeathScreen
@onready var hud := $Interface/HUD as HUD
@onready var debug_ui := $Interface/Debug
@onready var bgm := $Audio/BGMEventEmitter as FmodEventEmitter2D
@onready var ui_reset_audio := $Interface/UIResetEventEmitter

func _ready() -> void:
	reset()

# The most important var in the game! Controls how long gameplay is generally. Player is 2 units high btw
const CURVE_MAX_X: float = 12000.0


# Curve resource describe how scroll speed and music change over time.
# Y pos is the scrolling speed, and music intensity increases occur at the 3 rightmost points.
var game_speed_curve := load("res://game_speed_curve.tres") as Curve
# Maximum Y position on the curve. Past this point we extrapolate.
const CURVE_SCROLL_MULT: float = 200.0

var curve_max_y = game_speed_curve.sample(CURVE_MAX_X)
# TODO: wrongo, probs line equation wrong below. just hardcoding!
# var curve_gradient = (curve_max_y - game_speed_curve.sample(0.95)) / (1 - 0.95)
var p1 = game_speed_curve.get_point_position(1)[0]
var p2 = game_speed_curve.get_point_position(2)[0]
var p3 = game_speed_curve.get_point_position(3)[0]
var posMax = 0

func _process(_delta: float) -> void:
	var pos = maxf(posMax, player.position.y)
		
	var p = pos / CURVE_MAX_X
	if pos <= CURVE_MAX_X:
		player.scroll_speed = game_speed_curve.sample(p) * CURVE_SCROLL_MULT
	else:
		# y = mx + c type shit
		player.scroll_speed = (pos - CURVE_MAX_X) * 0.05 + curve_max_y * CURVE_SCROLL_MULT
		
	if !beast.is_active and p > p1:
		beast.appear()
	
	# Note that music transitions intensities at ~0.5 and ~1. All other values are meaningless.
	var intensity: float
	if p < p1:
		intensity = 0.5 - ((p1 - p) / (p1 - 0 )) * 0.5
	elif p < p2:
		intensity = 1   - ((p2 - p) / (p2 - p1)) * 0.5
	else:
		intensity = 1
	
	bgm.set_parameter("Intensity", intensity)
		
	print("Scroll Speed: %f, Y Pos: %f, Y Pos / MAX: %f, BGM Intensity %f" % [
		player.scroll_speed, pos, p, intensity
	])


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
		get_tree().root.set_input_as_handled()
	

# Completely reset game state. Bye!
func reset() -> void:
	player.reset()
	get_tree().paused = false
	death_screen.close()
	pause_menu.close()
	hud.show()
	ui_reset_audio.play_one_shot()
	beast.unappear()
	

func _on_player_on_reset() -> void:
	world.ResetWorld()


func _on_player_on_death() -> void:
	var tree := get_tree()
	tree.paused = true
	death_screen.open(player.position.y)
	get_tree().root.set_input_as_handled()


func _on_player_on_start_death() -> void:
	hud.hide()
