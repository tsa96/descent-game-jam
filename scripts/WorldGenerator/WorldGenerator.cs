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
// TileMapLayer (cell setting is by far the most expensive step). Completed chunks
// are pushed to a queue, where the _process calls on the main thread then attaches
// the chunk's TileMapLayer to the World scene.
public partial class WorldGenerator : Node2D
{
	const int ChunkWidth = 40;
	const int ChunkHeight = 32;
	const int ChunkExtraHeight = 8;
	const int ChunkExtraDiff = ChunkHeight - ChunkExtraHeight;
	const int ChunkBigHeight = ChunkHeight + ChunkExtraHeight * 2;
	const int ChunkHalfWidth = ChunkWidth / 2;
	const int InitialChunks = 4;
	const int ChunksFromBottomToTriggerGeneration = 4;
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

	const float HallucinationSpawnChance = 0.00125f;
	static PackedScene Hallucination1Scene = GD.Load<PackedScene>("res://entities/hallucinations/hallucination.tscn");
	static PackedScene Hallucination2Scene = GD.Load<PackedScene>("res://entities/hallucinations/hallucination2.tscn");

	CharacterBody2D Player;

	int ChunkCount = 0;
	int BottomChunk = 0;
	int RequestedChunks = 0;

	Node2D LayerContainer;
	static TileSet TileMapLayerTileSet;
	static Vector2 TileMapScale;
	ConcurrentQueue<Chunk> ChunkQueue = new();
	CancellationTokenSource ChunkThreadCts = new();

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
		TileMapLayerTileSet = layerTemplate.TileSet;
		TileMapScale = layerTemplate.Scale;

		InitializeSettings();
		ResetWorld();

		// Run generation on .NET taskpool thread, pushes completed Layers to queue on completion
		Task.Run(ProcessLayerQueue, ChunkThreadCts.Token);

		base._Ready();
	}

	public override void _Process(double delta)
	{
		// When we get close to bottom, start triggering more
		int yPos = (int)Player.Position.Y;
		if (yPos > (ChunkCount - ChunksFromBottomToTriggerGeneration) * ChunkHeight)
		{
			ChunkCount++;
			RequestedChunks++;
		}

		// Add newly generated layers to scene
		while (ChunkQueue.TryDequeue(out Chunk chunk))
		{
			LayerContainer.AddChild(chunk.TileMapLayer);
			chunk.TileMapLayer.Position = new Vector2(0, BottomChunk * ChunkHeight * TileSize);
			SpawnEntities(chunk);
			BottomChunk++;
		}

		// TODO: Old layer cleanup, previous stuff was buggy.

		base._Process(delta);
	}

	public void SpawnEntities(Chunk chunk)
	{
		for (int x = -ChunkHalfWidth; x < ChunkHalfWidth; x++)
		for (int y = 0; y < ChunkHeight; y++)
		{
			var coords = new Vector2I(x, y);
			int cell = chunk.TileMapLayer.GetCellSourceId(coords);
			if (cell != -1)
				continue;

			int left = chunk.TileMapLayer.GetCellSourceId(
				chunk.TileMapLayer.GetNeighborCell(coords, TileSet.CellNeighbor.LeftSide)
			);
			int right = chunk.TileMapLayer.GetCellSourceId(
				chunk.TileMapLayer.GetNeighborCell(coords, TileSet.CellNeighbor.RightSide)
			);
			int up = chunk.TileMapLayer.GetCellSourceId(
				chunk.TileMapLayer.GetNeighborCell(coords, TileSet.CellNeighbor.TopSide)
			);
			int down = chunk.TileMapLayer.GetCellSourceId(
				chunk.TileMapLayer.GetNeighborCell(coords, TileSet.CellNeighbor.BottomSide)
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
					(coords.Y + BottomChunk * ChunkHeight) * TileSize
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
			else if ((left == -1 && right == -1 && up == -1 && down == -1) && spawnChance < HallucinationSpawnChance)
			{
				var entityInstance =
					Random.Randf() < 0.5
						? Hallucination1Scene.Instantiate<Node2D>()
						: Hallucination2Scene.Instantiate<Node2D>();
				entityInstance.Position = new Vector2I(
					coords.X * TileSize,
					(coords.Y + BottomChunk * ChunkHeight) * TileSize
				);
				var animationPlayer = entityInstance.GetNode<AnimationPlayer>("AnimationPlayer");
				animationPlayer?.Play("loop");
				EntityContainer.AddChild(entityInstance);
			}
		}
	}

	public void ResetWorld()
	{
		// Stall whilst any generation finishes. Other thread shares some state unsafely with
		// us (>:D), could crash if we clear out the moles in this thread.
		// This shouldn't be noticeable, unless we have a mole looping forever (that would've
		// happened in single-threaded version anyway, who cares).
		while (RequestedChunks > 0) { }

		// Reset noise
		SeedNoise();

		ChunkQueue.Clear();

		foreach (TileMapLayer tml in LayerContainer.GetChildren().OfType<TileMapLayer>())
			tml.QueueFree();

		foreach (Node2D entity in EntityContainer.GetChildren().OfType<Node2D>())
			entity.QueueFree();

		Moles.Clear();
		LastChunk = null;

		// Start generating again on next frame to ensure everything is cleaned up
		Callable
			.From(() =>
			{
				for (int i = 0; i < MoleStartCount.Value; i++)
				{
					var mole = new Mole();

					// First mole should always start in centre.
					if (i == 0)
						mole.X = ChunkHalfWidth - 1;

					Moles.Add(mole);
				}

				// Setting this will get generator thread going
				LastChunk = null;
				RequestedChunks = InitialChunks;
				BottomChunk = 0;
				ChunkCount = 0;
			})
			.CallDeferred();
	}

	static Chunk LastChunk = null;

	void ProcessLayerQueue()
	{
		while (!ChunkThreadCts.IsCancellationRequested)
		{
			while (RequestedChunks > 0)
			{
				ulong startTime = Time.GetTicksMsec();

				Chunk chunk = new Chunk
				{
					Cells = LastChunk is not null ? CloneCells(LastChunk.Cells) : null,
					NextChunk = LastChunk?.NextChunk
				};

				chunk.Generate();
				LastChunk = chunk;
				ChunkQueue.Enqueue(chunk);
				RequestedChunks--;

				GD.Print($"Generated chunk in {Time.GetTicksMsec() - startTime}ms");
			}
		}
	}

	protected override void Dispose(bool disposing)
	{
		ChunkThreadCts.Cancel();
		base.Dispose(disposing);
	}
}
