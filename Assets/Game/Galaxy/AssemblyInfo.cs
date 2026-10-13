using System.Runtime.CompilerServices;
using Unity.Entities;

// Game.Galaxy: see docs/01-architecture.md §3 for what this assembly may reference.
[assembly: InternalsVisibleTo("Game.Tests.EditMode")]
[assembly: InternalsVisibleTo("Game.Tests.PlayMode")]
[assembly: InternalsVisibleTo("Game.Editor")]
[assembly: InternalsVisibleTo("Game.DevTools.Editor")]
[assembly: RegisterGenericComponentType(typeof(Game.Data.Components.ManagedEventQueue<Game.Core.Events.OriginShifted>))]
