using System.Globalization;
using Godot;

public partial class WorldGenerator
{
    static DynamicIntVariable Seed;
    static DynamicIntVariable ChunksToGenerate;
    static DynamicFloatVariable AirThresold;
    static DynamicIntVariable SideMargin;
    static DynamicIntVariable MoleStartCount;
    static DynamicFloatVariable MoleSpawnChance;
    static DynamicFloatVariable MoleMergeChance;
    static DynamicIntVariable MoleHoleSize;
    static DynamicFloatVariable MoleHoleFalloff;
    static DynamicFloatVariable MolePreviousDirMult;
    static DynamicBoolVariable MoleUseNormalDist;
    static DynamicFloatVariable MoleNormalSigma;
    static DynamicIntVariable ManglerNoiseType;
    static DynamicFloatVariable ManglerFrequency;
    static DynamicFloatVariable ManglerStrengthMult;
    static DynamicIntVariable MuncherIters;
    static DynamicIntVariable MuncherAirNeighbours;
    static DynamicIntVariable MuncherRockNeighbours;

    void InitializeSettings()
    {
        DebugContainer = GetNode<GridContainer>("../Interface/Debug/GridContainer");

        _perfLabel = GetNode<Label>("../Interface/Debug/GridContainer/PerfLabel");
        _regenButton = GetNode<Button>("../Interface/Debug/GridContainer/RegenButton");
        _regenButton.Pressed += Generate;

        // if Empty / 0 we generate a random seed
        Seed = new DynamicIntVariable("Seed", 0);
        ChunksToGenerate = new DynamicIntVariable("Chunks", 1);
        // Bit pointless as Mole works best when setting everything to very high strength
        AirThresold = new DynamicFloatVariable("Air Threshold", 0.5f);
        SideMargin = new DynamicIntVariable("Side Margin", 0);
        MoleStartCount = new DynamicIntVariable("Mole Start Count", 2);
        MoleSpawnChance = new DynamicFloatVariable("Mole Spawn Chance", 0.01f);
        // Logic for this needs more work for < 1 chance to work well, might be able to perform check earlier in iter?
        // Always merging seems to work really well though.
        MoleMergeChance = new DynamicFloatVariable("Mole Merge Chance", 1.0f);
        // If lower than 3 we can get sections that are very awkward to fit through.
        // If lowering, decrease muncher neighbour mins.
        MoleHoleSize = new DynamicIntVariable("Mole Hole Size", 3);
        // Meh, bit useless
        MoleHoleFalloff = new DynamicFloatVariable("Mole Hole Falloff", 0.1f);
        MolePreviousDirMult = new DynamicFloatVariable("Mole Previous Dir Mult", 0.6f);
        // Disabling this and upping previous dir mult gives extra snakiness but might be a bit much,
        // normal dist is good at encouraging moles downwards. Seems like mangler noise is just as good
        // for extra snaking anyway!
        MoleUseNormalDist = new DynamicBoolVariable("Mole Direction Use Normal Distribution", true);
        // See https://homepage.divms.uiowa.edu/~mbognar/applets/normal.html to play around with distribution!
        MoleNormalSigma = new DynamicFloatVariable("Mole Direction Normal Sigma", 1f);
        // 5 = Value. Changing to Perlin (3) is decent as well but value with a high frequency
        // gives nice solid lumps. If using Perlin, up frequency a lot.
        ManglerNoiseType = new DynamicIntVariable("Mangler Noise Type", 5);
        ManglerFrequency = new DynamicFloatVariable("Mangler Frequency", 0.2f);
        // This makes major difference to how tight our caves are, lets us keep hole size
        // relatively low (e.g. 3) and consistently gives us gaps you can fit through comfortably
        ManglerStrengthMult = new DynamicFloatVariable("Mangler Strength Mult", 3.5f);
        // More iters smooth out the terrain, but are the main performance hit of the entire generator (currently).
        // Might be able to speed up in the future with small chunks and better cache locality, idk.
        MuncherIters = new DynamicIntVariable("Muncher Iterations", 5);
        // Generally, keep air slightly lower (e.g. 1) than rock.
        MuncherAirNeighbours = new DynamicIntVariable("Muncher Air Neighbours Min", 4);
        MuncherRockNeighbours = new DynamicIntVariable("Muncher Rock Neighbours Min", 5);
    }

    class DynamicIntVariable
    {
        public int Value { get; set; }

        public DynamicIntVariable(string name, int def)
        {
            Value = def;
            DebugContainer.AddChild(new Label { Text = name });

            var settingPanel = new LineEdit { Text = def.ToString() };
            settingPanel.TextChanged += text => Value = int.TryParse(text, out int parsed) ? parsed : 0;
            DebugContainer.AddChild(settingPanel);
        }
    }

    class DynamicFloatVariable
    {
        public float Value { get; private set; }

        public DynamicFloatVariable(string name, float def)
        {
            Value = def;
            DebugContainer.AddChild(new Label { Text = name });

            var settingPanel = new LineEdit { Text = def.ToString(CultureInfo.InvariantCulture) };
            settingPanel.TextChanged += text => Value = float.TryParse(text, out float parsed) ? parsed : 0;
            DebugContainer.AddChild(settingPanel);
        }
    }

    class DynamicBoolVariable
    {
        public bool Value { get; private set; }

        public DynamicBoolVariable(string name, bool def)
        {
            Value = def;
            DebugContainer.AddChild(new Label { Text = name });

            var settingPanel = new CheckBox { ButtonPressed = def };
            settingPanel.Toggled += pressed => Value = pressed;
            DebugContainer.AddChild(settingPanel);
        }
    }
}
