using System.Runtime.CompilerServices;

// Game.Tests.Common: the shared test kit (SPEC-71). Its own tests live in Game.Tests.EditMode/TestKit.
[assembly: InternalsVisibleTo("Game.Tests.EditMode")]
[assembly: InternalsVisibleTo("Game.Tests.PlayMode")]
