extends Node2D

@onready var appear_audio_emitter := $Audio/MonsterAppearAudioEventEmitter as FmodEventEmitter2D
@onready var roar_audio_emitter := $Audio/MonsterRoarAudioEventEmitter as FmodEventEmitter2D

var player_inside: Player = null

func appear() -> void:
	appear_audio_emitter.play_one_shot()

func roar() -> void:
	roar_audio_emitter.play_one_shot()

func _physics_process(delta: float) -> void:
	if player_inside != null:
		player_inside.sanity_gain(-2.0 * delta)

func _on_area_2d_body_entered(body: Node2D) -> void:
	var player = (body as Player)
	if player != player:
		player_inside = player

func _on_area_2d_body_exited(body: Node2D) -> void:
	var player = (body as Player)
	if player != player:
		player_inside = null
