using Core.Collections.Scoped;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation.Entities;
using Game.Placement.Data;
using Game.Placement.MapManipulation;
using Game.Placement.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Map.Layout;

#nullable disable
public class UltraBeltProcessor<TPlacement, TDescriptor, TGlobalPivot, TGlobalTransform, TCoordinate, TVector, TDirection, TLocalPivot, TEntityModel, TConnector, TConnection, TInput, TOutput> : 
  IPlacementProcessor,
  IPathUpgradeProcessor
  where TPlacement : IEntityPlacement<TDescriptor>
  where TDescriptor : IEntityDescriptor<TGlobalTransform>
  where TGlobalPivot : struct, IPivot<TCoordinate, TDirection, TGlobalPivot>, IConvertibleToLocalPivot<TGlobalTransform, TLocalPivot>
  where TGlobalTransform : unmanaged, ITransform<TCoordinate, TGlobalTransform>
  where TCoordinate : unmanaged, IDiscreteCoordinate<TCoordinate, TVector, TDirection>
  where TVector : IDirectionComposableVector<TVector, TDirection>, IConvertibleToGlobalPosition<TGlobalTransform, TCoordinate>
  where TDirection : unmanaged, IDirection<TDirection>
  where TLocalPivot : struct, IPivot<TVector, TDirection, TLocalPivot>, IConvertibleToGlobalPivot<TGlobalTransform, TGlobalPivot>
  where TEntityModel : IEntityLayoutModel<TDescriptor, TCoordinate, TGlobalPivot, TGlobalTransform, TConnector, TConnection>
  where TConnector : IEntityConnectorModel<TGlobalPivot>
  where TConnection : IEntityConnectionModel<TGlobalPivot>
  where TInput : IEntityConnector
  where TOutput : IEntityConnector
{
  private readonly IMatchingDefinitionFinder<TDescriptor, TGlobalPivot> MatchingDefinitionFinder;
  private readonly IEntityPlacementAdapter<TPlacement, TDescriptor, TGlobalTransform, TCoordinate> PlacementAdapter;
  private readonly IEntityAccessorAdapter<TDescriptor, TCoordinate, TGlobalPivot, TGlobalTransform, TEntityModel, TConnector, TConnection> EntityMapAccessorAdapter;

  public UltraBeltProcessor(
    IMatchingDefinitionFinder<TDescriptor, TGlobalPivot> matchingDefinitionFinder,
    IEntityPlacementAdapter<TPlacement, TDescriptor, TGlobalTransform, TCoordinate> placementDefinitionSolver,
    IEntityAccessorAdapter<TDescriptor, TCoordinate, TGlobalPivot, TGlobalTransform, TEntityModel, TConnector, TConnection> entityMapAccessorAdapter)
  {
    this.MatchingDefinitionFinder = matchingDefinitionFinder;
    this.PlacementAdapter = placementDefinitionSolver;
    this.EntityMapAccessorAdapter = entityMapAccessorAdapter;
  }

  public void Process(
    IPlacementData placementData,
    PlacementInputHolder placementInput,
    IMapModel realMap,
    IReadOnlyMapLayoutModel virtualMap,
    IPlacementErrors placementErrors)
  {
    PlacementAsPartOfMap extender = new PlacementAsPartOfMap(placementData, new PlacementMapLayoutQuerySource[3]
    {
      PlacementMapLayoutQuerySource.Map,
      PlacementMapLayoutQuerySource.ValidPlacement,
      PlacementMapLayoutQuerySource.InvalidPlacement
    });
    IReadOnlyMapLayoutModel model = realMap.Layout.ExtendMapLayout((IMapLayoutExtender) extender).ExtendMapLayout<NotchConnectors>().ExtendMapLayout<ExtendedPlacementConnectors>().ToModel();
    using (ScopedList<TPlacement> entitiesDescriptors = ScopedList<TPlacement>.Get())
    {
      this.PlacementAdapter.GetAllInvalidEntities(placementData, (ICollection<TPlacement>) entitiesDescriptors);
      foreach (TPlacement placement1 in (List<TPlacement>) entitiesDescriptors)
      {
        try
        {
          if (this.MatchingDefinitionFinder.Definitions.Contains<IEntityDefinition>(placement1.Descriptor.Definition))
          {
            using (ScopedList<TEntityModel> intersectingEntities1 = ScopedList<TEntityModel>.Get())
            {
              this.GetIntersectionsWithMap(model, placement1, (IList<TEntityModel>) intersectingEntities1);
              if (intersectingEntities1.Count == 1)
              {
                TEntityModel entityModel = intersectingEntities1[0];
                if (this.MatchingDefinitionFinder.Definitions.Contains<IEntityDefinition>(entityModel.Definition))
                {
                  using (ScopedHashSet<TGlobalPivot> inputsToRemove = ScopedHashSet<TGlobalPivot>.Get())
                  {
                    using (ScopedHashSet<TGlobalPivot> outputsToRemove = ScopedHashSet<TGlobalPivot>.Get())
                    {
                      for (int connectionIndex = 0; connectionIndex < entityModel.NumConnectors; ++connectionIndex)
                      {
                        TConnection connection = entityModel.GetConnection(connectionIndex);
                        if (!connection.IsConnected)
                        {
                          switch (connection.FromConnector.Connector)
                          {
                            case TInput _:
                              inputsToRemove.Add(connection.FromConnector.Pivot);
                              continue;
                            case TOutput _:
                              outputsToRemove.Add(connection.FromConnector.Pivot);
                              continue;
                            default:
                              continue;
                          }
                        }
                      }
                      TDescriptor entity1;
                      TDescriptor upgradedEntity;
                      if (this.MatchingDefinitionFinder.TryDowngradingEntity(entityModel.ToDescriptor(), (IReadOnlyCollection<TGlobalPivot>) inputsToRemove, (IReadOnlyCollection<TGlobalPivot>) outputsToRemove, out entity1))
                      {
                        if (!this.MatchingDefinitionFinder.TryCombiningEntities(entity1, placement1.Descriptor, out upgradedEntity))
                          continue;
                      }
                      else
                        upgradedEntity = placement1.Descriptor;
                      using (ScopedList<TPlacement> intersectingEntities2 = ScopedList<TPlacement>.Get())
                      {
                        this.GetInvalidEntityIntersectionsWithPlacement(placementData, placement1, (IList<TPlacement>) intersectingEntities2);
                        if (intersectingEntities2.Count == 1)
                        {
                          TPlacement entity2 = intersectingEntities2[0];
                          TPlacement placement2 = this.PlacementAdapter.CreatePlacement(upgradedEntity, PlacementAllowability.ValidPlacement, entity2.PlacementIndex);
                          if (this.EntitiesOccupySameTiles(entity2.Descriptor, upgradedEntity))
                          {
                            this.PlacementAdapter.RemoveEntity(placementData, placement1);
                            this.PlacementAdapter.RemoveEntity(placementData, entity2);
                            this.PlacementAdapter.AddEntity(placementData, placement2);
                          }
                        }
                        else
                        {
                          if (intersectingEntities2.Count != 0)
                            break;
                          TPlacement placement3 = this.PlacementAdapter.CreatePlacement(upgradedEntity, PlacementAllowability.ValidPlacement, placement1.PlacementIndex);
                          this.PlacementAdapter.RemoveEntity(placementData, placement1);
                          this.PlacementAdapter.AddEntity(placementData, placement3);
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        catch (Exception ex)
        {
          this.PlacementAdapter.FlagError(placementErrors, ex, placement1.Descriptor.Transform.Position);
        }
      }
    }
  }

  private bool EntitiesOccupySameTiles(TDescriptor first, TDescriptor second)
  {
    IEntityConnectorData<TVector> entityConnectorData1 = first.Definition.CustomData.Get<IEntityConnectorData<TVector>>();
    IEntityConnectorData<TVector> entityConnectorData2 = second.Definition.CustomData.Get<IEntityConnectorData<TVector>>();
    if (entityConnectorData1.Tiles.Length != entityConnectorData2.Tiles.Length)
      return false;
    using (ScopedHashSet<TCoordinate> scopedHashSet1 = ScopedHashSet<TCoordinate>.Get())
    {
      using (ScopedHashSet<TCoordinate> scopedHashSet2 = ScopedHashSet<TCoordinate>.Get())
      {
        foreach (TVector tile in entityConnectorData1.Tiles)
        {
          TCoordinate global = tile.ToGlobal(first.Transform);
          scopedHashSet1.Add(global);
        }
        foreach (TVector tile in entityConnectorData2.Tiles)
        {
          TCoordinate global = tile.ToGlobal(second.Transform);
          scopedHashSet2.Add(global);
        }
        foreach (TCoordinate coordinate in (HashSet<TCoordinate>) scopedHashSet1)
        {
          if (!scopedHashSet2.Contains(coordinate))
            return false;
        }
        foreach (TCoordinate coordinate in (HashSet<TCoordinate>) scopedHashSet2)
        {
          if (!scopedHashSet1.Contains(coordinate))
            return false;
        }
        return true;
      }
    }
  }

  private void GetIntersectionsWithMap(
    IReadOnlyMapLayoutModel virtualMap,
    TPlacement invalidEntity,
    IList<TEntityModel> intersectingEntities)
  {
    foreach (TVector tile in invalidEntity.Descriptor.Definition.CustomData.Get<IEntityConnectorData<TVector>>().Tiles)
    {
      TCoordinate global = tile.ToGlobal(invalidEntity.Descriptor.Transform);
      TEntityModel entityModel;
      if (this.EntityMapAccessorAdapter.TryGetEntity(virtualMap, global, out entityModel) && !entityModel.ToDescriptor().Equals((IEntityDescriptor<TGlobalTransform>) invalidEntity.Descriptor))
        intersectingEntities.Add(entityModel);
    }
  }

  private void GetInvalidEntityIntersectionsWithPlacement(
    IPlacementData placementData,
    TPlacement invalidEntity,
    IList<TPlacement> intersectingEntities)
  {
    foreach (TVector tile in invalidEntity.Descriptor.Definition.CustomData.Get<IEntityConnectorData<TVector>>().Tiles)
    {
      TCoordinate global = tile.ToGlobal(invalidEntity.Descriptor.Transform);
      TPlacement placement;
      if (this.PlacementAdapter.TryGetFirstValidEntityAt(placementData, global, out placement) && !placement.Equals((object) invalidEntity))
        intersectingEntities.Add(placement);
    }
  }
}
