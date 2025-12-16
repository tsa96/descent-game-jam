class_name Player
extends CharacterBody2D

const SANITY_MAX = 100.0
var sanity: float

const WALK_SPEED = 240.0
const WALK_ACCEL = WALK_SPEED * 6.0
const DASH_SPEED = 700.0
const JUMP_VELOCITY = -525.0
const TERMINAL_VELOCITY = 500.0
const FAST_FALL_MULT = 0.7
const FAST_FALL_RECOVER_MULT = 1.5
var gravity: int = ProjectSettings.get(&"physics/2d/default_gravity")
var dash_charged: bool

@onready var platform_detector := $PlatformDetector as RayCast2D
@onready var animation_player := $AnimationPlayer as AnimationPlayer
@onready var sprite := $Sprite2D as Sprite2D
@onready var jump_sound := $Jump as AudioStreamPlayer2D
@onready var camera := $Camera as Camera2D

func _ready() -> void:
	reset()


func reset() -> void:
	sanity = SANITY_MAX
	velocity = Vector2(0, 0)
	position = Vector2(16, 0)
	dash_charged = false
	jump_sound.stop()


func _physics_process(delta: float) -> void:
	if is_on_floor():
		dash_charged = true
		
	# Jumping
	if Input.is_action_just_pressed("jump"):
		try_jump()
	elif Input.is_action_just_released("jump") and velocity.y < 0.0:
		# The player let go of jump early, reduce vertical momentum
		velocity.y *= 0.6
		
	# Falling
	if Input.is_action_pressed("fall"):
		# Fast-falling: allowed to exceed TV, acceleration even greater
		velocity.y = velocity.y + gravity * delta * FAST_FALL_MULT
	elif velocity.y > TERMINAL_VELOCITY - 0.001:
		# Speed exceeds TV; we must have just stopped fast-falling, decelerate towards TV
		velocity.y = velocity.y - gravity * delta * FAST_FALL_RECOVER_MULT
	else:
		# Regular falling, accelarate towards to TV
		velocity.y = minf(TERMINAL_VELOCITY, velocity.y + gravity * delta)	

	# A/D movement
	var direction := Input.get_axis("move_left", "move_right")
	velocity.x = move_toward(velocity.x, direction * WALK_SPEED, WALK_ACCEL * delta)

	# Dash
	if Input.is_action_just_pressed("dash") and dash_charged and direction != 0:
		velocity.x = direction * DASH_SPEED
		dash_charged = false

	if not is_zero_approx(velocity.x):
		if velocity.x > 0.0:
			sprite.scale.x = 1.0
		else:
			sprite.scale.x = -1.0

	floor_stop_on_slope = not platform_detector.is_colliding()
	move_and_slide()

	var animation := get_new_animation()
	if animation != animation_player.current_animation:
		animation_player.play(animation)


func get_new_animation() -> String:
	var animation_new: String
	if is_on_floor():
		if absf(velocity.x) > 0.1:
			animation_new = "run"
		else:
			animation_new = "idle"
	else:
		if velocity.y > 0.0:
			animation_new = "falling"
		else:
			animation_new = "jumping"
	return animation_new


func try_jump() -> void:
	if is_on_floor():
		velocity.y = JUMP_VELOCITY
		jump_sound.play()
