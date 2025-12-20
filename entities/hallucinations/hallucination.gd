extends Area2D

@onready var audio_emitter := $"../Audio/HallucinationAudioEventEmitter" as FmodEventEmitter2D

func _on_body_entered(body: Node2D) -> void:
	if body.name == "Player":
		(body as Player).sanity_drain(10)
		audio_emitter.play_one_shot()
		queue_free()
