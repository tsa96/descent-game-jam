extends Control

@onready var player := $"../../../World/Player" as Player
@onready var bar_inner := $BarOuter/BarInner
@onready var amount := $BarOuter/Amount
@onready var max_size = bar_inner.size

func _process(_delta: float) -> void:
	bar_inner.size = Vector2((player.sanity / 100) * max_size[0], max_size[1])
	amount.text = "%d/100" % player.sanity
