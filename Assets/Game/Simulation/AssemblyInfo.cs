using System.Runtime.CompilerServices;
using Game.Core.Events;
using Game.Data.Components;
using Unity.Entities;

// Game.Simulation: see docs/01-architecture.md §3 for what this assembly may reference.
[assembly: InternalsVisibleTo("Game.Tests.EditMode")]
[assembly: InternalsVisibleTo("Game.Tests.PlayMode")]
[assembly: InternalsVisibleTo("Game.Editor")]
[assembly: InternalsVisibleTo("Game.DevTools.Editor")]
[assembly: RegisterGenericComponentType(typeof(ManagedEventQueue<ShipDestroyed>))]
