using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;

// Multithreaded, deterministic, chunking world gen powered by...
//
// - THE MOLE:    Weighted random tunnels downwards through chunks
// - THE MANGLER: Apply random noise on top of generate mole tunnels
// - THE MUNCHER: Cellular automata that eats away at rocks with surrounding air
//
// Generation runs on a dedicated .NET taskpool thread as player falls through the
// world, working with 2D float arrays, then setting cells on a new, un-parented
// TileMapLayer (cell setting is by far the most expensive step). Completed layers
// are pushed to a queue, where the _process calls on the main thread then attaches
// the layer to the World scene.
public partial class WorldGenerator : Node2D
{
	const int ChunkWidth = 40;
	const int ChunkHeight = 32;
	const int ChunkExtraHeight = 8;
	const int ChunkExtraDiff = ChunkHeight - ChunkExtraHeight;
	const int ChunkBigHeight = ChunkHeight + ChunkExtraHeight * 2;
	const int ChunkHalfWidth = ChunkWidth / 2;
	const int InitialLayers = 1;
	const int ChunksPerLayer = 24;
	const int LayerHeight = ChunkHeight * ChunksPerLayer;
	const int LayersFromBottomToTriggerGeneration = 4;
	const int TileSize = 16;
	const int MaxMoles = 8;

	const float SanityItemSpawnChance = 0.02f;
	const float SanityRestorePreference = 0.7f;
	static PackedScene SanityRestoringScene = GD.Load<PackedScene>(
		"res://entities/sanity_restoring/sanity_restoring.tscn"
	);
	static PackedScene SanityDrainingScene = GD.Load<PackedScene>(
		"res://entities/sanity_draining/sanity_draining.tscn"
	);

	const float HallucinationSpawnChance = 0.00025f;
	static PackedScene Hallucination1Scene = GD.Load<PackedScene>("res://entities/hallucinations/hallucination.tscn");
	static PackedScene Hallucination2Scene = GD.Load<PackedScene>("res://entities/hallucinations/hallucination2.tscn");

	CharacterBody2D Player;

	int LayerCount = 0;
	int BottomLayer = 0;
	int RequestedLayers = 0;
	int CleanupTracker = 0;

	Node2D LayerContainer;
	static TileSet LayerTileSet;
	static Vector2 LayerScale;
	ConcurrentQueue<Layer> LayerQueue = new();
	CancellationTokenSource LayerThreadCts = new();
	Dictionary<int, Layer> Layers = new();

	Node2D EntityContainer;

	static readonly RandomNumberGenerator Random = new();
	static readonly FastNoiseLite ManglerNoise = new();

	static readonly List<Mole> Moles = [];

	public override void _Ready()
	{
		Player = GetNode<CharacterBody2D>("Player");
		LayerContainer = GetNode<Node2D>("TileMapContainer");
		EntityContainer = GetNode<Node2D>("EntityContainer");

		// Can't access .Scale from tp thread for some reason (but we can with tileset ??)
		var layerTemplate = GetNode<TileMapLayer>("TileMapTemplateLayer");
		LayerTileSet = layerTemplate.TileSet;
		LayerScale = layerTemplate.Scale;

		InitializeSettings();
		ResetWorld();

		// Run generation on .NET taskpool thread, pushes completed Layers to queue on completion
		Task.Run(ProcessLayerQueue, LayerThreadCts.Token);

		base._Ready();
	}

	public override void _Process(double delta)
	{
		// When we get close to bottom, start triggering more
		int yPos = (int)Player.Position.Y;
		if (yPos > (LayerCount - LayersFromBottomToTriggerGeneration) * LayerHeight)
		{
			LayerCount++;
			RequestedLayers++;
		}

		// Add newly generated layers to scene
		while (LayerQueue.TryDequeue(out Layer layer))
		{
			LayerContainer.AddChild(layer.TileMapLayer);
			layer.TileMapLayer.Position = new Vector2(0, BottomLayer * LayerHeight * TileSize);
			Layers.Add(BottomLayer, layer);

			for (int x = -ChunkHalfWidth; x < ChunkHalfWidth; x++)
			for (int y = 0; y < LayerHeight; y++)
			{
				var coords = new Vector2I(x, y);
				int cell = layer.TileMapLayer.GetCellSourceId(coords);
				if (cell != -1)
					continue;

				int left = layer.TileMapLayer.GetCellSourceId(
					layer.TileMapLayer.GetNeighborCell(coords, TileSet.CellNeighbor.LeftSide)
				);
				int right = layer.TileMapLayer.GetCellSourceId(
					layer.TileMapLayer.GetNeighborCell(coords, TileSet.CellNeighbor.RightSide)
				);
				int up = layer.TileMapLayer.GetCellSourceId(
					layer.TileMapLayer.GetNeighborCell(coords, TileSet.CellNeighbor.TopSide)
				);
				int down = layer.TileMapLayer.GetCellSourceId(
					layer.TileMapLayer.GetNeighborCell(coords, TileSet.CellNeighbor.BottomSide)
				);

				float spawnChance = Random.Randf();

				if ((left != -1 || right != -1) && up == -1 && spawnChance < SanityItemSpawnChance)
				{
					var entityInstance =
						Random.Randf() < SanityRestorePreference
							? SanityRestoringScene.Instantiate<Node2D>()
							: SanityDrainingScene.Instantiate<Node2D>();
					entityInstance.Position = new Vector2I(
						coords.X * TileSize,
						(coords.Y + BottomLayer * LayerHeight) * TileSize
					);
					// Flip sprite if attached to left wall
					if (left != -1)
					{
						var sprite = entityInstance.GetNode<Sprite2D>("Sprite2D");
						if (sprite != null)
							sprite.FlipH = true;
					}
					EntityContainer.AddChild(entityInstance);
				}
				else if (
					(left == -1 && right == -1 && up == -1 && down == -1)
					&& spawnChance < HallucinationSpawnChance
				)
				{
					var entityInstance =
						Random.Randf() < 0.5
							? Hallucination1Scene.Instantiate<Node2D>()
							: Hallucination2Scene.Instantiate<Node2D>();
					entityInstance.Position = new Vector2I(
						coords.X * TileSize,
						(coords.Y + BottomLayer * LayerHeight) * TileSize
					);
					var animationPlayer = entityInstance.GetNode<AnimationPlayer>("AnimationPlayer");
					animationPlayer?.Play("loop");
					EntityContainer.AddChild(entityInstance);
				}
			}

			BottomLayer++;
		}

		// TODO: Old layer cleanup, previous stuff was buggy.

		base._Process(delta);
	}

	public void ResetWorld()
	{
		// Stall whilst any generation finishes. Other thread shares some state unsafely with
		// us (>:D), could crash if we clear out the moles in this thread.
		// This shouldn't be noticeable, unless we have a mole looping forever (that would've
		// happened in single-threaded version anyway, who cares).
		while (RequestedLayers > 0) { }

		// Reset noise
		SeedNoise();

		LayerQueue.Clear();

		foreach (TileMapLayer tml in LayerContainer.GetChildren().OfType<TileMapLayer>())
			tml.QueueFree();

		foreach (Node2D entity in EntityContainer.GetChildren().OfType<Node2D>())
			entity.QueueFree();

		Layers.Clear();
		Moles.Clear();

		// Start generating again on next frame to ensure everything is cleaned up
		Callable
			.From(() =>
			{
				// Setting this will get generator thread going
				RequestedLayers = InitialLayers;
				BottomLayer = 0;
				LayerCount = 0;
			})
			.CallDeferred();
	}

	void ProcessLayerQueue()
	{
		while (!LayerThreadCts.IsCancellationRequested)
		{
			while (RequestedLayers > 0)
			{
				ulong startTime = Time.GetTicksMsec();

				Layer layer = Layer.Generate();
				LayerQueue.Enqueue(layer);
				RequestedLayers--;

				GD.Print($"Generated layer in {Time.GetTicksMsec() - startTime}ms");
			}
		}
	}

	void DestroyBlocks(Vector2I pos, int strength)
	{
		int x = ((pos.X / TileSize) + ChunkHalfWidth);
		int y = (pos.Y + 12) / TileSize;

		// TODO: This won't work on layer boundaries.
		// Still wrong past first layer!! Maths seems right, don't fuckin know
		Layer layer = Layers[(pos.Y + 12) / TileSize];
		if (layer is null)
		{
			GD.PushWarning($"Failed to find TileMapLayer for pos X: ${x}, Y: ${y}");
			return;
		}

		// Erase cells in circle of radius `strength`
		int strengthSq = strength * strength;
		for (int dx = -strength; dx <= strength; dx++)
		for (int dy = -strength; dy <= strength; dy++)
		{
			if (dx * dx + dy * dy > strengthSq)
				continue;

			int nx = x + dx;
			int ny = y + dy;
			if (nx < 0 || ny < 0 || nx >= ChunkWidth || ny >= ChunkHeight)
				continue;

			layer.TileMapLayer.EraseCell(new Vector2I(nx - ChunkHalfWidth, ny));
		}
	}

	protected override void Dispose(bool disposing)
	{
		LayerThreadCts.Cancel();
		base.Dispose(disposing);
	}
}
