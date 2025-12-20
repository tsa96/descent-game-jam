extends Area2D

@onready var appear_audio_emitter := $Audio/MonsterAppearAudioEventEmitter as FmodEventEmitter2D
@onready var roar_audio_emitter := $Audio/MonsterRoarAudioEventEmitter as FmodEventEmitter2D

var player_inside: Player = null
var is_active: bool = false

func _ready() -> void:
	# TODO: Appear later
	appear()

func appear() -> void:
	appear_audio_emitter.play_one_shot()
	is_active = true

func roar() -> void:
	roar_audio_emitter.play_one_shot()

func _physics_process(_delta: float) -> void:
	if not is_active:
		return
	
	if player_inside != null:
		player_inside.sanity_gain(-2.0)

func _on_body_entered(body: Node2D) -> void:
	var player = (body as Player)
	if player != null:
		player_inside = player

func _on_body_exited(body: Node2D) -> void:
	var player = (body as Player)
	if player != null:
		player_inside = null
