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
using Renderer = UltraBelt.BeltBridgeSimulationRenderer;
using Simulation = UltraBelt.BeltBridgeSimulation;
using RendererData = UltraBelt.IBeltBridgeDrawData;

namespace UltraBelt;

// At 5/6/26, only god and I knew what this code meant
// Now only god knows
internal class BeltBridgeBuilding
{
    private readonly BuildingDefinitionId _defId = new("ub-belt-bridge");
    private readonly ILogger _logger;

    internal BeltBridgeBuilding(ILogger logger)
    {
        _logger = logger;

        IBuildingGroupBuilder bldingGroup = BuildingGroup.Create(new BuildingDefinitionGroupId("ub-belt-bridge-group"))
            .WithTitle("building-variant.ub-belt-bridge.title".T())
            .WithDescription("building-variant.ub-belt-bridge.description".T())
            .WithIcon(FileTextureLoader.LoadTextureAsSprite(Main.Res.SubPath("belt-bridge.png"), out _))
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
            .DynamicallyRendering<BeltBridgeSimulationRenderer, BeltBridgeSimulation, IBeltBridgeDrawData>(
                new BeltBridgeDrawData())
            .WithStaticDrawData(BeltBridgeDrawData.CreateDrawData())
            .WithoutSound()
            .WithoutSimulationConfiguration()
            .WithEfficiencyData(new BuildingEfficiencyData(2, 2));

        AtomicBuildings.Extend()
            .AllScenarios()
            .WithBuilding(blding, bldingGroup)
            .UnlockedAtMilestone(new ByIndexMilestoneSelector(new Index(0)))
            .WithDefaultPlacement()
            .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(0).ChildAt(^1).InsertAfter())
            .WithSimulation(new BeltBridgeFactoryBuilder(), _logger)
            .WithAtomicShapeProcessingModules(BuiltinResearchSpeed.BeltSpeed, 1.0f)
            .WithPrediction(new BeltBridgePredictionFactoryBuilder(), logger)
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
            Direction_L = TileDirection.South.Value,
            StandType = BuildingBeltStandType.Normal,
            IOType = BuildingItemIOType.ElevatedBorder,
            Seperators = false
        };

        var outputUnder = new BuildingItemOutput
        {
            Position_L = new TileVector(0, 0, 0),
            Direction_L = TileDirection.East.Value,
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


    internal AtomicStatefulBuildingSimulationSystem<BeltBridgeSimulation, BeltBridgeSimulationState> Register(
    SimulationSystemsDependencies dependencies)
    {
        var builder = new BeltBridgeFactoryBuilder();
        IFactory<BeltBridgeSimulationState, BeltBridgeSimulation> factory =
            builder.BuildFactory(dependencies, out var config);

        return new AtomicStatefulBuildingSimulationSystem<BeltBridgeSimulation, BeltBridgeSimulationState>(
            factory, _defId, _logger);
    }
}

internal interface IBeltBridgeDrawData : IBuildingCustomDrawData
{
    public IBeltLaneRendererDefinition InputUnderRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(-0.5f, 0.0f, 0.0f),
        new LocalVector(0.0f, 0.0f, 0.0f));

    public IBeltLaneRendererDefinition OutputUnderRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(0.0f, 0.0f, 0.0f),
        new LocalVector(0.5f, 0.0f, 0.0f));

    public IBeltLaneRendererDefinition InputOverRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(0.0f, 0.5f, 0.0f),
        new LocalVector(0.0f, 0.0f, 0.0f));

    public IBeltLaneRendererDefinition OutputOverRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(0.0f, 0.0f, 0.0f),
        new LocalVector(0.0f, -0.5f, 0.0f));
}

internal class BeltBridgeDrawData : IBeltBridgeDrawData
{
    internal static BuildingDrawData CreateDrawData()
    {
        var baseMeshPath = Main.Res.SubPath("UltraBelt.fbx");
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

internal class BeltBridgeConfiguration : IBeltBridgeConfiguration
{
    public BeltSpeed BeltSpeed => _Speed;

    public BeltDelay ProcessingDelay => _Delay;
    private readonly BuffableBeltDelay _Delay;

    private readonly BuffableBeltSpeed _Speed;

    public BeltBridgeConfiguration(
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

public interface IBeltBridgeConfiguration
{
    public BeltSpeed BeltSpeed { get; }
    public BeltDelay ProcessingDelay { get; }
}

internal class BeltBridgeFactoryBuilder
    : IBuildingSimulationFactoryBuilder<BeltBridgeSimulation, BeltBridgeSimulationState,
        BeltBridgeConfiguration>
{
    public IFactory<BeltBridgeSimulationState, BeltBridgeSimulation> BuildFactory(
        SimulationSystemsDependencies dependencies,
        out BeltBridgeConfiguration config)
    {
        config = new BeltBridgeConfiguration(
            BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
            BuffableBeltDelay.DiscreteDuration.OnePointFiveSeconds,
            new ResearchSpeedId("BeltSpeed"));

        var bridge = new ShapeOperationBridge(
            dependencies.Mode.MaxShapeLayers,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);

        return new BeltBridgeSimulationFactory(config, dependencies.ShapeRegistry, bridge);
    }
}

internal class BeltBridgePredictionFactoryBuilder
    : IBuildingPredictionFactoryBuilder<Processing2In2OutPredictionSimulation>
{
    public IFactory<Processing2In2OutPredictionSimulation> BuildFactory(PredictionSystemsDependencies dependencies)
    {
        var op = new ShapeOperationBridge(
            dependencies.Mode.MaxShapeLayers,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);
        return new Processing2In2OutPredictionSimulationFactory(op);
    }
}

public class BeltBridgeSimulation : Simulation<BeltBridgeSimulationState>, IItemSimulation, IUpdatableSimulation
{
    public readonly BeltLane InputUnder;
    public readonly BeltLane InputOver;
    public readonly BeltLane OutputUnder;
    public readonly BeltLane OutputOver;

    /// <inheritdoc />
    public int NumItemReceivers => 2;

    /// <inheritdoc />
    public int NumItemProviders => 2;

    public BeltBridgeSimulation(
        BeltBridgeSimulationState simulationState,
        IBeltBridgeConfiguration bridgeConfiguration,
        IShapeRegistry shapeRegistry,
        ShapeOperationBridge bridge) : base(simulationState)
    {
        OutputUnder = new BeltLane(bridgeConfiguration.BeltSpeed, simulationState.OutputUnderState);
        OutputOver = new BeltLane(bridgeConfiguration.BeltSpeed, simulationState.OutputOverState);
        InputUnder = new BeltLane(bridgeConfiguration.BeltSpeed, simulationState.InputUnderState, OutputUnder);
        InputOver = new BeltLane(bridgeConfiguration.BeltSpeed, simulationState.InputOverState, OutputOver);
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


[SyncableIdentifier("BridgeState")]
public class BeltBridgeSimulationState : ISimulationState
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

public class BeltBridgeSimulationFactory : IFactory<BeltBridgeSimulationState, BeltBridgeSimulation>
{
    private readonly IBeltBridgeConfiguration Configuration;
    private readonly ShapeOperationBridge Bridge;
    private readonly IShapeRegistry ShapeRegistry;

    public BeltBridgeSimulationFactory(
        IBeltBridgeConfiguration configuration,
        IShapeRegistry shapeRegistry,
        ShapeOperationBridge bridge)
    {
        Configuration = configuration;
        ShapeRegistry = shapeRegistry;
        Bridge = bridge;
    }

    public BeltBridgeSimulation Produce(BeltBridgeSimulationState simulationState)
    {
        return new BeltBridgeSimulation(simulationState, Configuration, ShapeRegistry, Bridge);
    }
}

internal class BeltBridgeSimulationRenderer
    : StatelessBuildingSimulationRenderer<BeltBridgeSimulation, IBeltBridgeDrawData>
{
    public BeltBridgeSimulationRenderer(
        IMapModel map,
        IBuildingSoundManager soundManager,
        IShapeRegistry shapeRegistry) : base(map) { }

    public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
    {
        BeltBridgeSimulation simulation = entity.Simulation;

        DrawBeltItem(entity.Transform, options, simulation.InputUnder, entity.DrawData.InputUnderRenderingDefinition);
        DrawBeltItem(entity.Transform, options, simulation.InputOver, entity.DrawData.InputOverRenderingDefinition);
        DrawBeltItem(entity.Transform, options, simulation.OutputUnder, entity.DrawData.OutputUnderRenderingDefinition);
        DrawBeltItem(entity.Transform, options, simulation.OutputOver, entity.DrawData.OutputOverRenderingDefinition);
    }
}

public class ShapeOperationBridge : ShapeOperation<ShapeDefinition, ShapeBridgeResult>, IItemOperation2In2Out
{
    private readonly int MaxShapeLayers;

    public ShapeOperationBridge(
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

        ShapeBridgeResult shapeCutResult = Execute(shapeItem.Definition);
        output1 = shapeCutResult.LeftSide != null ? ShapeRegistry.GetItem(shapeCutResult.LeftSide.Shape) : (IItem)null;

        ShapeBridgeResult shapeCutResult2 = Execute(shapeItem2.Definition);
        output2 = shapeCutResult2.LeftSide != null ? ShapeRegistry.GetItem(shapeCutResult2.LeftSide.Shape) : (IItem)null;

        return true;
    }

    public override ShapeBridgeResult ExecuteInternal(ShapeDefinition shape)
    {
        ShapeLogic.UnfoldResult unfolded = ShapeLogic.Unfold(shape.Layers);
        var firstSide = unfolded.References.Where(reference => reference.PartIndex % 1 == 0).ToList();

        ShapeCollapseResult leftResult = ShapeLogic.Collapse(
            firstSide,
            shape.PartCount,
            MaxShapeLayers,
            ShapeIdManager,
            unfolded.FusedReferences);
        return new ShapeBridgeResult(leftResult);
    }
}

public readonly struct ShapeBridgeResult
{
    public readonly ShapeCollapseResult LeftSide;

    public ShapeBridgeResult(ShapeCollapseResult leftSide)
    {
        LeftSide = leftSide;
    }
}
