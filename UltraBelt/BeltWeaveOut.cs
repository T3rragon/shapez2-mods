using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Core.Events;
using Core.Collections;
using Core.Factory;
using Core.Localization;
using Game.Core.Coordinates;
using Game.Core.Research;
using Game.Core.Simulation;
using Game.Core.Serialization;
using Game.Content.Features.Predictions.Processing;
using JetBrains.Annotations;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Hijack;
using ShapezShifter.Hijack.Predictions;
using ShapezShifter.Kit;
using ShapezShifter.Textures;
using UnityEngine;
using ILogger = Core.Logging.ILogger;
using Renderer = UltraBelt.BeltWeaveOutSimulationRenderer;
using Simulation = UltraBelt.BeltWeaveOutSimulation;
using RendererData = UltraBelt.IBeltWeaveOutDrawData;

namespace UltraBelt;

// At 5/6/26, only god and I knew what this code meant
// Now only god knows
internal class BeltWeaveOutBuilding
{
    private readonly BuildingDefinitionId _defId = new("ub-belt-weaveout");
    private readonly ILogger _logger;

    internal BeltWeaveOutBuilding(ILogger logger)
    {
        _logger = logger;

        IBuildingGroupBuilder bldingGroup = BuildingGroup.Create(new BuildingDefinitionGroupId("ub-belt-weaveout-group"))
            .WithTitle("building-variant.ub-belt-weaveout.title".T())
            .WithDescription("building-variant.ub-belt-weaveout.description".T())
            .WithIcon(FileTextureLoader.LoadTextureAsSprite(Main.Res.SubPath("belt-weaveout.png"), out _))
            .AsTransportableBuilding()
            .WithPreferredPlacement(DefaultPreferredPlacementMode.LinePerpendicular)
            .WithDefaultStructureOverview();

        /* IBuildingConnectorData connectorData = BuildingConnectors.SingleTile()
            .AddShapeInput(ShapeConnectorConfig.CustomInput(TileDirection.West))
            .AddShapeInput(ShapeConnectorConfig.CustomInput(TileDirection.South))
            .AddShapeOutput(ShapeConnectorConfig.CustomOutput(TileDirection.East))
            .AddShapeOutput(ShapeConnectorConfig.CustomOutput(TileDirection.North))
            .Build();
            */

        IBuildingConnectorData connectorData = QuickPatchConnectorData();

        IBuildingBuilder blding = Building.Create(_defId)
            .WithConnectorData(connectorData)
            .DynamicallyRendering<BeltWeaveOutSimulationRenderer, BeltWeaveOutSimulation, IBeltWeaveOutDrawData>(
                new BeltWeaveOutDrawData())
            .WithStaticDrawData(BeltWeaveOutDrawData.CreateDrawData())
            .WithoutSound()
            .WithoutSimulationConfiguration()
            .WithEfficiencyData(new BuildingEfficiencyData(2, 2));

        AtomicBuildings.Extend()
            .AllScenarios()
            .WithBuilding(blding, bldingGroup)
            .UnlockedAtMilestone(new ByIndexMilestoneSelector(new Index(0)))
            .WithDefaultPlacement()
            .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(0).ChildAt(^1).InsertAfter())
            .WithSimulation(new BeltWeaveOutFactoryBuilder(), _logger)
            .WithAtomicShapeProcessingModules(BuiltinResearchSpeed.BeltSpeed, 1.0f)
            .WithPrediction(new BeltWeaveOutPredictionFactoryBuilder(), logger)
            .Build();
    }

    public void Dispose() { }

    private SideUpgradePresentationData CreateSideUpgradePresentationData(string titleId, string titleDescription)
    {
        return new SideUpgradePresentationData(
            new ResearchUpgradeId("Patience"),
            GameImageId.Empty,
            GameVideoId.Empty,
            titleId.T(),
            titleDescription.T(),
            false,
            "Buildings");
    }

    // Taken from SpicyDRK's FourWaySplitter
    private static IBuildingConnectorData QuickPatchConnectorData()
    {
        var inputUnder = new BuildingItemInput
        {
            Position_L = new TileVector(0, 0, 0),
            Direction_L = TileDirection.West.Value,
            StandType = BuildingBeltStandType.Normal,
            IOType = BuildingItemIOType.ElevatedBorder,
            Seperators = false
        };

        var inputOver = new BuildingItemInput
        {
            Position_L = new TileVector(0, 0, 0),
            Direction_L = TileDirection.East.Value,
            StandType = BuildingBeltStandType.Normal,
            IOType = BuildingItemIOType.ElevatedBorder,
            Seperators = false
        };

        var outputUnder = new BuildingItemOutput
        {
            Position_L = new TileVector(0, 0, 0),
            Direction_L = TileDirection.South.Value,
            StandType = BuildingBeltStandType.Normal,
            IOType = BuildingItemIOType.ElevatedBorder,
            Seperators = false
        };

        var outputOver = new BuildingItemOutput
        {
            Position_L = new TileVector(0, 0, 0),
            Direction_L = TileDirection.North.Value,
            StandType = BuildingBeltStandType.Normal,
            IOType = BuildingItemIOType.ElevatedBorder,
            Seperators = false
        };

        var allConnectors = new List<BuildingBaseIO>
            {
                inputUnder,
                inputOver,
                outputUnder,
                outputOver
            };

        TileVector[] tiles = { TileVector.Zero };
        LocalTileBounds tileBounds = new(min: TileVector.Zero, max: TileVector.Zero);
        TileDimensions tileDimensions = tileBounds.Dimensions;
        LocalVector tileBoundsCenter = LocalVector.Lerp(
            a: (LocalVector)tileBounds.Min,
            b: (LocalVector)tileBounds.Max,
            t: 0.5f);

        return new BuildingConnectorData(
            allInputs: allConnectors,
            tiles: tiles,
            tileBounds: tileBounds,
            tileBoundsCenter: tileBoundsCenter,
            tileDimensions: tileDimensions);
    }


    internal AtomicStatefulBuildingSimulationSystem<BeltWeaveOutSimulation, BeltWeaveOutSimulationState> Register(
    SimulationSystemsDependencies dependencies)
    {
        var builder = new BeltWeaveOutFactoryBuilder();
        IFactory<BeltWeaveOutSimulationState, BeltWeaveOutSimulation> factory =
            builder.BuildFactory(dependencies, out var config);

        return new AtomicStatefulBuildingSimulationSystem<BeltWeaveOutSimulation, BeltWeaveOutSimulationState>(
            factory, _defId, _logger);
    }
}

internal interface IBeltWeaveOutDrawData : IBuildingCustomDrawData
{
    public IBeltLaneRendererDefinition InputUnderRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(-0.5f, 0.0f, 0.0f),
        new LocalVector(0.0f, 0.0f, 0.0f));

    public IBeltLaneRendererDefinition OutputUnderRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(0.0f, 0.0f, 0.0f),
        new LocalVector(0.0f, 0.5f, 0.0f));

    public IBeltLaneRendererDefinition InputOverRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(0.5f, 0.0f, 0.0f),
        new LocalVector(0.0f, 0.0f, 0.0f));

    public IBeltLaneRendererDefinition OutputOverRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(0.0f, 0.0f, 0.0f),
        new LocalVector(0.0f, -0.5f, 0.0f));
}

internal class BeltWeaveOutDrawData : IBeltWeaveOutDrawData
{
    internal static BuildingDrawData CreateDrawData()
    {
        var baseMeshPath = Main.Res.SubPath("FourSplit.fbx");
        Mesh baseMesh = FileMeshLoader.LoadSingleMeshFromFile(baseMeshPath);
        LOD6Mesh baseModLod = MeshLod.Create().AddLod0Mesh(baseMesh).BuildLod6Mesh();

        var empty = new LODEmptyMesh();

        return new BuildingDrawData(
            renderVoidBelow: false,
            new ILODMesh[] { baseModLod, baseModLod, baseModLod },
            baseModLod,
            baseModLod,
            baseModLod.LODClose,
            new LODEmptyMesh(),
            BoundingBoxHelper.CreateBasicCollider(baseMesh),
            new BeltBridgeDrawData(),
            false,
            null,
            false);
    }
}

internal class BeltWeaveOutConfiguration : IBeltWeaveOutConfiguration
{
    public BeltSpeed BeltSpeed => _Speed;

    public BeltDelay ProcessingDelay => _Delay;
    private readonly BuffableBeltDelay _Delay;

    private readonly BuffableBeltSpeed _Speed;

    public BeltWeaveOutConfiguration(
        BuffableBeltSpeed.DiscreteSpeed beltSpeed,
        BuffableBeltDelay.DiscreteDuration cutDuration,
        ResearchSpeedId researchSpeed)
    {
        _Speed = new BuffableBeltSpeed
        {
            BaseSpeed = beltSpeed,
            ResearchId = researchSpeed
        };

        _Delay = new BuffableBeltDelay
        {
            BaseDuration = cutDuration,
            Research = researchSpeed
        };

        _Speed.OnAfterDeserialize();
        _Delay.OnAfterDeserialize();
    }
}

public interface IBeltWeaveOutConfiguration
{
    public BeltSpeed BeltSpeed { get; }
    public BeltDelay ProcessingDelay { get; }
}

internal class BeltWeaveOutFactoryBuilder
    : IBuildingSimulationFactoryBuilder<BeltWeaveOutSimulation, BeltWeaveOutSimulationState,
        BeltWeaveOutConfiguration>
{
    public IFactory<BeltWeaveOutSimulationState, BeltWeaveOutSimulation> BuildFactory(
        SimulationSystemsDependencies dependencies,
        out BeltWeaveOutConfiguration config)
    {
        config = new BeltWeaveOutConfiguration(
            BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
            BuffableBeltDelay.DiscreteDuration.OnePointFiveSeconds,
            new ResearchSpeedId("BeltSpeed"));

        var weaveout = new ShapeOperationWeaveOut(
            dependencies.Mode.MaxShapeLayers,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);

        return new BeltWeaveOutSimulationFactory(config, dependencies.ShapeRegistry, weaveout);
    }
}

internal class BeltWeaveOutPredictionFactoryBuilder
    : IBuildingPredictionFactoryBuilder<Processing2In2OutPredictionSimulation>
{
    public IFactory<Processing2In2OutPredictionSimulation> BuildFactory(PredictionSystemsDependencies dependencies)
    {
        var op = new ShapeOperationWeaveOut(
            dependencies.Mode.MaxShapeLayers,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);
        return new Processing2In2OutPredictionSimulationFactory(op);
    }
}

public class BeltWeaveOutSimulation : Simulation<BeltWeaveOutSimulationState>, IItemSimulation, IUpdatableSimulation
{
    public readonly BeltLane InputUnder;
    public readonly BeltLane InputOver;
    public readonly BeltLane OutputUnder;
    public readonly BeltLane OutputOver;

    /// <inheritdoc />
    public int NumItemReceivers => 2;

    /// <inheritdoc />
    public int NumItemProviders => 2;

    public BeltWeaveOutSimulation(
        BeltWeaveOutSimulationState simulationState,
        IBeltWeaveOutConfiguration weaveoutConfiguration,
        IShapeRegistry shapeRegistry,
        ShapeOperationWeaveOut weaveout) : base(simulationState)
    {
        OutputUnder = new BeltLane(weaveoutConfiguration.BeltSpeed, simulationState.OutputUnderState);
        OutputOver = new BeltLane(weaveoutConfiguration.BeltSpeed, simulationState.OutputOverState);
        InputUnder = new BeltLane(weaveoutConfiguration.BeltSpeed, simulationState.InputUnderState, OutputUnder);
        InputOver = new BeltLane(weaveoutConfiguration.BeltSpeed, simulationState.InputOverState, OutputOver);
    }

    /// <inheritdoc />
    public IItemReceiver GetItemReceiver(int index) => index == 0 ? InputUnder : InputOver;

    /// <inheritdoc />
    public IItemProvider GetItemProvider(int index) => index == 0 ? OutputUnder : OutputOver;

    /// <inheritdoc />
    public void TraverseLanes<TTraverser>(TTraverser traverser)
        where TTraverser : IItemLaneTraverser
    {
        traverser.Traverse(InputUnder);
        traverser.Traverse(InputOver);
        traverser.Traverse(OutputUnder);
        traverser.Traverse(OutputOver);
    }

    /// <inheritdoc />
    public void ClearContent()
    {
        TraverseLanes(ClearItemsItemLaneTraverser.Default);
    }

    /// <inheritdoc />
    public void Update(Ticks startTicks, Ticks deltaTicks)
    {
        OutputUnder.Update(deltaTicks);
        OutputOver.Update(deltaTicks);
        InputUnder.Update(deltaTicks);
        InputOver.Update(deltaTicks);
    }
}


[SyncableIdentifier("WeaveOutState")]
public class BeltWeaveOutSimulationState : ISimulationState
{
    public readonly BeltLaneState InputUnderState = new();
    public readonly BeltLaneState InputOverState = new();
    public readonly BeltLaneState OutputUnderState = new();
    public readonly BeltLaneState OutputOverState = new();

    public void Sync(ISerializationVisitor visitor)
    {
        InputUnderState.Sync(visitor);
        InputOverState.Sync(visitor);
        OutputUnderState.Sync(visitor);
        OutputOverState.Sync(visitor);
    }
}

public class BeltWeaveOutSimulationFactory : IFactory<BeltWeaveOutSimulationState, BeltWeaveOutSimulation>
{
    private readonly IBeltWeaveOutConfiguration Configuration;
    private readonly ShapeOperationWeaveOut WeaveOut;
    private readonly IShapeRegistry ShapeRegistry;

    public BeltWeaveOutSimulationFactory(
        IBeltWeaveOutConfiguration configuration,
        IShapeRegistry shapeRegistry,
        ShapeOperationWeaveOut weaveout)
    {
        Configuration = configuration;
        ShapeRegistry = shapeRegistry;
        WeaveOut = weaveout;
    }

    public BeltWeaveOutSimulation Produce(BeltWeaveOutSimulationState simulationState)
    {
        return new BeltWeaveOutSimulation(simulationState, Configuration, ShapeRegistry, WeaveOut);
    }
}

internal class BeltWeaveOutSimulationRenderer
    : StatelessBuildingSimulationRenderer<BeltWeaveOutSimulation, IBeltWeaveOutDrawData>
{
    public BeltWeaveOutSimulationRenderer(
        IMapModel map,
        IBuildingSoundManager soundManager,
        IShapeRegistry shapeRegistry) : base(map) { }

    public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
    {
        BeltWeaveOutSimulation simulation = entity.Simulation;

        DrawBeltItem(entity.Transform, options, simulation.InputUnder, entity.DrawData.InputUnderRenderingDefinition);
        DrawBeltItem(entity.Transform, options, simulation.InputOver, entity.DrawData.InputOverRenderingDefinition);
        DrawBeltItem(entity.Transform, options, simulation.OutputUnder, entity.DrawData.OutputUnderRenderingDefinition);
        DrawBeltItem(entity.Transform, options, simulation.OutputOver, entity.DrawData.OutputOverRenderingDefinition);
    }
}

public class ShapeOperationWeaveOut : ShapeOperation<ShapeDefinition, ShapeWeaveOutResult>, IItemOperation2In2Out
{
    private readonly int MaxShapeLayers;

    public ShapeOperationWeaveOut(
        int maxShapeLayers,
        IShapeRegistry shapeRegistry,
        IShapeIdManager shapeIdManager) : base(shapeRegistry, shapeIdManager)
    {
        MaxShapeLayers = maxShapeLayers;
    }

    public bool TryExecute(IItem input1, IItem input2, out IItem output1, out IItem output2)
    {
        if (input1 is not ShapeItem shapeItem || input2 is not ShapeItem shapeItem2)
        {
            output1 = null;
            output2 = null;
            return false;
        }

        ShapeWeaveOutResult shapeCutResult = Execute(shapeItem.Definition);
        output1 = shapeCutResult.LeftSide != null ? ShapeRegistry.GetItem(shapeCutResult.LeftSide.Shape) : (IItem)null;

        ShapeWeaveOutResult shapeCutResult2 = Execute(shapeItem2.Definition);
        output2 = shapeCutResult2.LeftSide != null ? ShapeRegistry.GetItem(shapeCutResult2.LeftSide.Shape) : (IItem)null;

        return true;
    }

    public override ShapeWeaveOutResult ExecuteInternal(ShapeDefinition shape)
    {
        ShapeLogic.UnfoldResult unfolded = ShapeLogic.Unfold(shape.Layers);
        var firstSide = unfolded.References.Where(reference => reference.PartIndex % 1 == 0).ToList();

        ShapeCollapseResult leftResult = ShapeLogic.Collapse(
            firstSide,
            shape.PartCount,
            MaxShapeLayers,
            ShapeIdManager,
            unfolded.FusedReferences);
        return new ShapeWeaveOutResult(leftResult);
    }
}

public readonly struct ShapeWeaveOutResult
{
    public readonly ShapeCollapseResult LeftSide;

    public ShapeWeaveOutResult(ShapeCollapseResult leftSide)
    {
        LeftSide = leftSide;
    }
}
