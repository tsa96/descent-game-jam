using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;

// Multithreaded, deterministic, chunking world gen powered by...

// - THE MOLE:    Weighted random tunnels downwards through chunks
// - THE MANGLER: Apply random noise on top of generate mole tunnels
// - THE MUNCHER: Cellular automata that eats away at rocks with surrounding air
//
// Generation runs on a dedicated .NET taskpool thread as player falls through the
// world, working with 2D float arrays,// then setting cells on a new, un-parented
// TileMapLayer (cell setting is by far the most expensive step). Completed layers
// are pushed to a queue, where the _process calls on the main thread then attaches
// the layer to the World scene.
public partial class WorldGenerator : Node2D
{
	const int ChunkWidth = 32;
	const int ChunkHeight = 32;
	const int ChunkExtraHeight = 8;
	const int ChunkExtraDiff = ChunkHeight - ChunkExtraHeight;
	const int ChunkBigHeight = ChunkHeight + ChunkExtraHeight * 2;
	const int ChunkHalfWidth = ChunkWidth / 2;
	const int InitialLayers = 4;
	const int ChunksPerLayer = 8;
	const int LayerHeight = ChunkHeight * ChunksPerLayer;
	const int LayersFromBottomToTriggerGeneration = 4;
	const int TileSize = 16;
	const int MaxMoles = 8;

	CharacterBody2D Player;

	int LayerCount = 0;
	int BottomLayer = 0;
	int RequestedLayers = 0;
	int CleanupTracker = 0;

	Node2D LayerContainer;
	static TileMapLayer LayerTemplate;
	ConcurrentQueue<TileMapLayer> LayerQueue = new();
	CancellationTokenSource LayerThreadCts = new();

	static readonly RandomNumberGenerator Random = new();
	static readonly FastNoiseLite ManglerNoise = new();

	static readonly List<Mole> Moles = [];

	public override void _Ready()
	{
		Player = GetNode<CharacterBody2D>("Player");
		LayerContainer = GetNode<Node2D>("TileMapContainer");
		LayerTemplate = GetNode<TileMapLayer>("TileMapTemplateLayer");
		LayerTemplate.Enabled = false;

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
		while (LayerQueue.TryDequeue(out var tileMapLayer))
		{
			LayerContainer.AddChild(tileMapLayer);
			tileMapLayer.Position = new Vector2(0, BottomLayer * LayerHeight * TileSize);
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
		// happened in singlethreaded version anyway, who cares).
		while (RequestedLayers > 0) { }

		// Reset noise
		SeedNoise();

		LayerQueue.Clear();

		foreach (TileMapLayer tml in LayerContainer.GetChildren().OfType<TileMapLayer>())
			tml.QueueFree();

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
				LayerQueue.Enqueue(layer.TileMapLayer);
				RequestedLayers--;

				GD.Print($"Generated layer in {Time.GetTicksMsec() - startTime}ms");
			}
		}
	}

	protected override void Dispose(bool disposing)
	{
		LayerThreadCts.Cancel();
		base.Dispose(disposing);
	}
}
