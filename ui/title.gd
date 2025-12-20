extends Node2D

@onready var title_sprite := $Title as Sprite2D
@onready var title_sprite_animator := $TitleAnimationPlayer as AnimationPlayer
@onready var ui_select_audio := $Audio/UISelectEventEmitter

func _ready() -> void:
	title_sprite_animator.play("droop")

func _on_start_button_pressed() -> void:
	ui_select_audio.play_one_shot()
	get_tree().change_scene_to_file("res://game.tscn")

func _on_quit_button_pressed() -> void:
	ui_select_audio.play_one_shot()
	get_tree().quit()
