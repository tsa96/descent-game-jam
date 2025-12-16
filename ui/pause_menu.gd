class_name PauseMenu
extends Control

@onready var resume_button := $BackgroundShader/ColorRect/VBoxContainer/ResumeButton
@onready var audio_player: AudioStreamPlayer2D = get_parent().get_node("InterfaceAudioPlayer")

var open_sound := preload("res://assets/audio/pause.ogg")

func _ready() -> void:
	hide()

func close() -> void:
	get_tree().paused = false
	hide()

func open() -> void:
	show()
	resume_button.grab_focus()
	audio_player.stream = open_sound
	audio_player.play()

func _on_resume_button_pressed() -> void:
	close()

func _on_quit_button_pressed() -> void:
	if visible:
		get_tree().quit()
