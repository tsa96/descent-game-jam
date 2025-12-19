class_name Player
extends CharacterBody2D

const SANITY_MAX = 100.0
var sanity: float

const START_POS =  Vector2(-30 * 8, -32);

const WALK_SPEED = 240.0
const WALK_ACCEL = WALK_SPEED * 6.0
const DASH_SPEED = 700.0
const JUMP_VELOCITY = -525.0
const SMALL_JUMP_MULT = 0.4
const TERMINAL_VELOCITY = 500.0
const FAST_FALL_MULT = 0.7
const FAST_FALL_RECOVER_MULT = 2.5
const WALL_CLIMB_SLOWDOWN = 30
const WALL_JUMP_VELOCITY = -350.0
const WALL_JUMP_COOLDOWN = 1
const WALL_JUMP_MAX_SPEED = 250
const FOOTSTEP_AUDIO_TIMER_RESET = .3
const HIGH_LANDING_VELOCITY = 400.0

const IDLE_ANIM = "idle"
const WALK_ANIM = "walk"
const JUMPING_ANIM = "jump"
const FALLING_ANIM = "fall"
const FALLING_FAST_ANIM = "fastfall"
const DASH_ANIM = "dash"
const LANDING_ANIM = "land"

var gravity: int = ProjectSettings.get(&"physics/2d/default_gravity")
var dash_charged: bool
var last_walljump: float
var footstep_audio_timer: float
var regen_on: float

@onready var wall_jump_ray_left := $WallClimbRayLeft as RayCast2D
@onready var wall_jump_ray_right := $WallClimbRayRight as RayCast2D
@onready var player_animator := $PlayerAnimator as AnimationPlayer
@onready var player_sprite := $PlayerSprite as Sprite2D
@onready var camera := $Camera as Camera2D

@onready var footstep_audio_emitter := $Audio/FootstepAudioEventEmitter as FmodEventEmitter2D
@onready var land_high_vel_audio_emitter := $Audio/LandHighVelocityAudioEventEmitter as FmodEventEmitter2D
@onready var dash_audio_emitter := $Audio/DashAudioEventEmitter as FmodEventEmitter2D


func _ready() -> void:
	reset()


func reset() -> void:
	sanity = SANITY_MAX
	position = START_POS
	velocity = Vector2(0, 0)
	dash_charged = false
	last_walljump = 1000
	footstep_audio_timer = 0


func _physics_process(delta: float) -> void:
	if is_on_floor():
		dash_charged = true
		
	var direction := Input.get_axis("move_left", "move_right")
		
	# Wall climbing
	last_walljump += delta
	if Input.is_action_pressed("jump") and is_on_wall() and direction != 0 and last_walljump > WALL_JUMP_COOLDOWN:
		velocity.y = maxf(0, velocity.y - gravity * WALL_CLIMB_SLOWDOWN * delta)
		
	# Jumping
	if Input.is_action_just_pressed("jump"):
		print(velocity.y)
		if is_on_floor():
			velocity.y = JUMP_VELOCITY
			footstep_audio_emitter.play()
		elif is_close_to_wall() and velocity.y < WALL_JUMP_MAX_SPEED and last_walljump > WALL_JUMP_COOLDOWN:
			velocity.y = WALL_JUMP_VELOCITY
			footstep_audio_emitter.play()
			last_walljump = 0
	elif Input.is_action_just_released("jump") and velocity.y < 0.0:
		# The player let go of jump early, reduce vertical momentum
		velocity.y *= SMALL_JUMP_MULT
		
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
	velocity.x = move_toward(velocity.x, direction * WALK_SPEED, WALK_ACCEL * delta)

	# Dash
	var just_dashed: bool = false
	if Input.is_action_just_pressed("dash") and dash_charged and direction != 0:
		velocity.x = direction * DASH_SPEED
		dash_charged = false
		sanity -= 20
		regen_on = false
		just_dashed = true
		$Regen.start()

	var prev_fall_speed := abs(velocity.y) as float
	var was_on_ground := is_on_floor() as bool
	move_and_slide()
	
	var just_landed := not was_on_ground and is_on_floor() as bool
	var just_fell := was_on_ground and not is_on_floor() as bool
	
	play_character_audio(delta, just_dashed, just_fell, just_landed, prev_fall_speed)
	play_animation(direction, just_dashed, just_landed)
		
	if sanity > SANITY_MAX: 
		sanity = SANITY_MAX
	
	# Sanity Regen
	if sanity < SANITY_MAX and regen_on:
		sanity += 0.1
	
	# If you are falling at terminal velocity, sanity will go down
	if velocity.y == TERMINAL_VELOCITY:
		sanity -= 0.5
		regen_on = false
		$Regen.start()
	
	# The same but if you're falling faster it'll go down faster
	if velocity.y > TERMINAL_VELOCITY:
		sanity -= velocity.y * 0.0005
		regen_on = false
		$Regen.start()
	
	# Death
	if sanity <= 0:
		reset()
		
	print(sanity)


func play_character_audio(delta: float, just_dashed: bool, just_fell: bool, just_landed: bool, prev_fall_speed: float = 0.0) -> void:
	if just_dashed:
		dash_audio_emitter.play_one_shot()
	
	if just_landed and prev_fall_speed >= HIGH_LANDING_VELOCITY:
		land_high_vel_audio_emitter.play_one_shot()
	elif (is_on_floor() and velocity.x > 0.1) or just_fell or just_landed:
		if footstep_audio_timer <= 0.0:
			footstep_audio_emitter.play_one_shot()
			footstep_audio_timer = FOOTSTEP_AUDIO_TIMER_RESET
		else:
			footstep_audio_timer -= delta
	else:
		footstep_audio_timer = 0


func play_animation(direction: float, just_dashed: bool, just_landed: bool) -> void:
	if not is_zero_approx(direction):
		if direction > 0.0:
			player_sprite.scale.x = 1.0
		else:
			player_sprite.scale.x = -1.0
	
	var animation: String
	
	if just_dashed:
		animation = DASH_ANIM
	elif just_landed:
		animation = LANDING_ANIM
	elif is_on_floor():
		if absf(velocity.x) > 0.1:
			animation = WALK_ANIM
		else:
			animation = IDLE_ANIM
	else:
		if velocity.y > 0.0:
			if velocity.y >= HIGH_LANDING_VELOCITY:
				animation = FALLING_FAST_ANIM
			else:
				animation = FALLING_ANIM
		else:
			animation = JUMPING_ANIM
	
	var cur_animation: String = player_animator.current_animation
	if cur_animation == animation:
		return
	
	if player_animator.is_playing() and (cur_animation == DASH_ANIM or cur_animation == LANDING_ANIM) and (animation != LANDING_ANIM and animation != JUMPING_ANIM):
		# Let dash/land anim play fully before continuing with walk/fall. Prevents those from squashing these anims
		return
	
	player_animator.play(animation)

func is_close_to_wall() -> bool:
	# is_on_wall is too tight, extremely hard to do a walljump in the opposite direction
	# before sideways movement makes the check fail - use raycasts instead (configure in 2D view!)
	return wall_jump_ray_left.is_colliding() or wall_jump_ray_right.is_colliding()


func _on_regen_timeout() -> void:
	regen_on = true
