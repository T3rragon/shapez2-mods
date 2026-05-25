using System.Collections.Generic;
using System.Linq;
using Core.Logging;
using Game.Orchestration;
using MonoMod.RuntimeDetour;
using ShapezShifter.Kit;
using ShapezShifter.Hijack;
using ShapezShifter.SharpDetour;

namespace UltraBelt;

public class Main : IMod
{
    internal static readonly ModFolderLocator Res = ModDirectoryLocator.CreateLocator<Main>().SubLocator("Resources");
    private readonly Hook _modSystemHook;
    private readonly BeltBridgeBuilding _bridge;
    private readonly BeltWeaveInBuilding _weavein;
    private readonly BeltWeaveOutBuilding _weaveout;

    public Main(ILogger logger)
    {
        _bridge = new BeltBridgeBuilding(logger);
        _weavein = new BeltWeaveInBuilding(logger);
        _weaveout = new BeltWeaveOutBuilding(logger);

        _modSystemHook = DetourHelper
            .CreatePostfixHook<BuiltinSimulationSystems, IEnumerable<ISimulationSystem>>(
                simulationSystems => simulationSystems.CreateSimulationSystems(),
                CreateModSystems);
    }

    public void Dispose()
    {
        _modSystemHook.Dispose();
    }

    private IEnumerable<ISimulationSystem> CreateModSystems(
        BuiltinSimulationSystems builtinSimulationSystems,
        IEnumerable<ISimulationSystem> systems)
    {
        SimulationSystemsDependencies dependencies = new SimulationSystemsDependencies(builtinSimulationSystems);
        return systems
            .Append(_bridge.Register(dependencies))
            .Append(_weavein.Register(dependencies))
            .Append(_weaveout.Register(dependencies));
    }
}
