class_name Beast
extends Area2D

@onready var appear_audio_emitter := $Audio/MonsterAppearAudioEventEmitter as FmodEventEmitter2D
@onready var roar_audio_emitter := $Audio/MonsterRoarAudioEventEmitter as FmodEventEmitter2D
@onready var animator := $AnimationPlayer as AnimationPlayer
@onready var main_sprite := $Sprite2D as Sprite2D

var player_inside: Player = null
var is_active: bool = false

func _ready() -> void:
	main_sprite.modulate.a = 0


func appear() -> void:
	animator.play("appear")
	appear_audio_emitter.play_one_shot()
	visible = true
	is_active = true


func unappear() -> void:
	visible = false
	is_active = false


func roar() -> void:
	roar_audio_emitter.play_one_shot()


func _physics_process(delta: float) -> void:
	if not is_active:
		return
	
	if player_inside == null:
		return
	
	print(delta)
	player_inside.sanity_gain(-2.0)


func _on_body_entered(body: Node2D) -> void:
	var player = (body as Player)
	if player != null:
		player_inside = player
		animator.play("eat", -1, 1.3)
		roar()


func _on_body_exited(body: Node2D) -> void:
	var player = (body as Player)
	if player != null:
		player_inside = null
