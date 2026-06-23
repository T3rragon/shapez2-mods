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
using Renderer = UltraBelt.BeltWeaveInSimulationRenderer;
using Simulation = UltraBelt.BeltWeaveInSimulation;
using RendererData = UltraBelt.IBeltWeaveInDrawData;

namespace UltraBelt;

// At 5/6/26, only god and I knew what this code meant
// Now only god knows
internal class BeltWeaveInBuilding
{
    private readonly BuildingDefinitionId _defId = new("ub-belt-weavein");
    private readonly ILogger _logger;

    internal BeltWeaveInBuilding(ILogger logger)
    {
        _logger = logger;

        IBuildingGroupBuilder bldingGroup = BuildingGroup.Create(new BuildingDefinitionGroupId("ub-belt-weavein-group"))
            .WithTitle("building-variant.ub-belt-weavein.title".T())
            .WithDescription("building-variant.ub-belt-weavein.description".T())
            .WithIcon(FileTextureLoader.LoadTextureAsSprite(Main.Res.SubPath("belt-weavein.png"), out _))
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
            .DynamicallyRendering<BeltWeaveInSimulationRenderer, BeltWeaveInSimulation, IBeltWeaveInDrawData>(
                new BeltWeaveInDrawData())
            .WithStaticDrawData(BeltWeaveInDrawData.CreateDrawData())
            .WithoutSound()
            .WithoutSimulationConfiguration()
            .WithEfficiencyData(new BuildingEfficiencyData(2, 2));

        AtomicBuildings.Extend()
            .AllScenarios()
            .WithBuilding(blding, bldingGroup)
            .UnlockedAtMilestone(new ByIndexMilestoneSelector(new Index(0)))
            .WithDefaultPlacement()
            .InToolbar(ToolbarElementLocator.Root().ChildAt(0).ChildAt(0).ChildAt(^1).InsertAfter())
            .WithSimulation(new BeltWeaveInFactoryBuilder(), _logger)
            .WithAtomicShapeProcessingModules(BuiltinResearchSpeed.BeltSpeed, 1.0f)
            .WithPrediction(new BeltWeaveInPredictionFactoryBuilder(), logger)
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
            Direction_L = TileDirection.North.Value,
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
            Direction_L = TileDirection.East.Value,
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


    internal AtomicStatefulBuildingSimulationSystem<BeltWeaveInSimulation, BeltWeaveInSimulationState> Register(
    SimulationSystemsDependencies dependencies)
    {
        var builder = new BeltWeaveInFactoryBuilder();
        IFactory<BeltWeaveInSimulationState, BeltWeaveInSimulation> factory =
            builder.BuildFactory(dependencies, out var config);

        return new AtomicStatefulBuildingSimulationSystem<BeltWeaveInSimulation, BeltWeaveInSimulationState>(
            factory, _defId, _logger);
    }
}

internal interface IBeltWeaveInDrawData : IBuildingCustomDrawData
{
    public IBeltLaneRendererDefinition InputUnderRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(-0.5f, 0.0f, 0.0f),
        new LocalVector(0.0f, 0.0f, 0.0f));

    public IBeltLaneRendererDefinition OutputUnderRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(0.0f, 0.0f, 0.0f),
        new LocalVector(0.0f, 0.5f, 0.0f));
    public IBeltLaneRendererDefinition InputOverRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(0.0f, -0.5f, 0.0f),
        new LocalVector(0.0f, 0.0f, 0.0f));

    public IBeltLaneRendererDefinition OutputOverRenderingDefinition => new MyBeltLaneRenderingDefinition(
        new LocalVector(0.0f, 0.0f, 0.0f),
        new LocalVector(0.5f, 0.0f, 0.0f));
}

internal class BeltWeaveInDrawData : IBeltWeaveInDrawData
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

internal class BeltWeaveInConfiguration : IBeltWeaveInConfiguration
{
    public BeltSpeed BeltSpeed => _Speed;

    public BeltDelay ProcessingDelay => _Delay;
    private readonly BuffableBeltDelay _Delay;

    private readonly BuffableBeltSpeed _Speed;

    public BeltWeaveInConfiguration(
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

public interface IBeltWeaveInConfiguration
{
    public BeltSpeed BeltSpeed { get; }
    public BeltDelay ProcessingDelay { get; }
}

internal class BeltWeaveInFactoryBuilder
    : IBuildingSimulationFactoryBuilder<BeltWeaveInSimulation, BeltWeaveInSimulationState,
        BeltWeaveInConfiguration>
{
    public IFactory<BeltWeaveInSimulationState, BeltWeaveInSimulation> BuildFactory(
        SimulationSystemsDependencies dependencies,
        out BeltWeaveInConfiguration config)
    {
        config = new BeltWeaveInConfiguration(
            BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
            BuffableBeltDelay.DiscreteDuration.OnePointFiveSeconds,
            new ResearchSpeedId("BeltSpeed"));

        var weavein = new ShapeOperationWeaveIn(
            dependencies.Mode.MaxShapeLayers,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);

        return new BeltWeaveInSimulationFactory(config, dependencies.ShapeRegistry, weavein);
    }
}

internal class BeltWeaveInPredictionFactoryBuilder
    : IBuildingPredictionFactoryBuilder<Processing2In2OutPredictionSimulation>
{
    public IFactory<Processing2In2OutPredictionSimulation> BuildFactory(PredictionSystemsDependencies dependencies)
    {
        var op = new ShapeOperationWeaveIn(
            dependencies.Mode.MaxShapeLayers,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);
        return new Processing2In2OutPredictionSimulationFactory(op);
    }
}

public class BeltWeaveInSimulation : Simulation<BeltWeaveInSimulationState>, IItemSimulation, IUpdatableSimulation
{
    public readonly BeltLane InputUnder;
    public readonly BeltLane InputOver;
    public readonly BeltLane OutputUnder;
    public readonly BeltLane OutputOver;

    /// <inheritdoc />
    public int NumItemReceivers => 2;

    /// <inheritdoc />
    public int NumItemProviders => 2;

    public BeltWeaveInSimulation(
        BeltWeaveInSimulationState simulationState,
        IBeltWeaveInConfiguration weaveinConfiguration,
        IShapeRegistry shapeRegistry,
        ShapeOperationWeaveIn weavein) : base(simulationState)
    {
        OutputUnder = new BeltLane(weaveinConfiguration.BeltSpeed, simulationState.OutputUnderState);
        OutputOver = new BeltLane(weaveinConfiguration.BeltSpeed, simulationState.OutputOverState);
        InputUnder = new BeltLane(weaveinConfiguration.BeltSpeed, simulationState.InputUnderState, OutputUnder);
        InputOver = new BeltLane(weaveinConfiguration.BeltSpeed, simulationState.InputOverState, OutputOver);
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


[SyncableIdentifier("WeaveInState")]
public class BeltWeaveInSimulationState : ISimulationState
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

public class BeltWeaveInSimulationFactory : IFactory<BeltWeaveInSimulationState, BeltWeaveInSimulation>
{
    private readonly IBeltWeaveInConfiguration Configuration;
    private readonly ShapeOperationWeaveIn WeaveIn;
    private readonly IShapeRegistry ShapeRegistry;

    public BeltWeaveInSimulationFactory(
        IBeltWeaveInConfiguration configuration,
        IShapeRegistry shapeRegistry,
        ShapeOperationWeaveIn weavein)
    {
        Configuration = configuration;
        ShapeRegistry = shapeRegistry;
        WeaveIn = weavein;
    }

    public BeltWeaveInSimulation Produce(BeltWeaveInSimulationState simulationState)
    {
        return new BeltWeaveInSimulation(simulationState, Configuration, ShapeRegistry, WeaveIn);
    }
}

internal class BeltWeaveInSimulationRenderer
    : StatelessBuildingSimulationRenderer<BeltWeaveInSimulation, IBeltWeaveInDrawData>
{
    public BeltWeaveInSimulationRenderer(
        IMapModel map,
        IBuildingSoundManager soundManager,
        IShapeRegistry shapeRegistry) : base(map) { }

    public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
    {
        BeltWeaveInSimulation simulation = entity.Simulation;

        DrawBeltItem(entity.Transform, options, simulation.InputUnder, entity.DrawData.InputUnderRenderingDefinition);
        DrawBeltItem(entity.Transform, options, simulation.InputOver, entity.DrawData.InputOverRenderingDefinition);
        DrawBeltItem(entity.Transform, options, simulation.OutputUnder, entity.DrawData.OutputUnderRenderingDefinition);
        DrawBeltItem(entity.Transform, options, simulation.OutputOver, entity.DrawData.OutputOverRenderingDefinition);
    }
}

public class ShapeOperationWeaveIn : ShapeOperation<ShapeDefinition, ShapeWeaveInResult>, IItemOperation2In2Out
{
    private readonly int MaxShapeLayers;

    public ShapeOperationWeaveIn(
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

        ShapeWeaveInResult shapeCutResult = Execute(shapeItem.Definition);
        output1 = shapeCutResult.LeftSide != null ? ShapeRegistry.GetItem(shapeCutResult.LeftSide.Shape) : (IItem)null;

        ShapeWeaveInResult shapeCutResult2 = Execute(shapeItem2.Definition);
        output2 = shapeCutResult2.LeftSide != null ? ShapeRegistry.GetItem(shapeCutResult2.LeftSide.Shape) : (IItem)null;

        return true;
    }

    public override ShapeWeaveInResult ExecuteInternal(ShapeDefinition shape)
    {
        ShapeLogic.UnfoldResult unfolded = ShapeLogic.Unfold(shape.Layers);
        var firstSide = unfolded.References.Where(reference => reference.PartIndex % 1 == 0).ToList();

        ShapeCollapseResult leftResult = ShapeLogic.Collapse(
            firstSide,
            shape.PartCount,
            MaxShapeLayers,
            ShapeIdManager,
            unfolded.FusedReferences);
        return new ShapeWeaveInResult(leftResult);
    }
}

public readonly struct ShapeWeaveInResult
{
    public readonly ShapeCollapseResult LeftSide;

    public ShapeWeaveInResult(ShapeCollapseResult leftSide)
    {
        LeftSide = leftSide;
    }
}
