namespace Game.Presentation.Services
{
    /// <summary>How a flight camera preset chooses its yaw.</summary>
    public enum FlightCameraYaw : byte
    {
        Config = 0,    // CameraSettings.YawMode, as SPEC-62 ships it
        Heading = 1,   // behind the ship; right-drag orbits away from it
        Aim = 2,       // turns towards the cursor when it leaves a dead zone ahead
        Free = 3       // only the mouse turns it (mouse look)
    }

    /// <summary>Feel prototype (owner, 2026-10-21: "the top down only view feels very restraining... can we implement a third person
    /// orbital camera that follows the cursor"). Live-switchable flight camera presets; `topdown` is the SPEC-62 config unchanged.
    /// Switch with the console: `camera preset=&lt;name&gt;`.</summary>
    public struct FlightCameraPreset
    {
        public string Name, Description;
        public bool UseConfig;
        public float DefaultPitchDeg, MinPitchDeg, MaxPitchDeg, DefaultDistance, MinDistance, MaxDistance, FovDeg;
        public float TiltUpDeg;              // the view looks this far above the orbit line: the ship sits low and the horizon shows
        public FlightCameraYaw Yaw;
        public float AimDeadZoneDeg, AimTurnGain, AimMaxTurnDegPerSecond;   // `cursor`: dead zone in % of the half-width, gain, top rate
        public bool MouseLook;               // the mouse always orbits, the cursor is locked and the reticle aims
        public float ReticleHeight;          // mouse look: the reticle's height as a fraction of the screen, from the bottom

        public static readonly FlightCameraPreset[] All =
        {
            new FlightCameraPreset { Name = "topdown", Description = "SPEC-62 as shipped: 55° above, follows the heading", UseConfig = true },
            new FlightCameraPreset
            {
                Name = "chase", Description = "low behind the ship, turns with it; right-drag to look around",
                DefaultPitchDeg = 16, MinPitchDeg = 6, MaxPitchDeg = 75, DefaultDistance = 75, MinDistance = 25, MaxDistance = 320,
                FovDeg = 50, TiltUpDeg = 8, Yaw = FlightCameraYaw.Heading
            },
            new FlightCameraPreset
            {
                Name = "cursor", Description = "low and free of the heading; turns while the cursor is near a side of the screen",
                DefaultPitchDeg = 22, MinPitchDeg = 6, MaxPitchDeg = 75, DefaultDistance = 95, MinDistance = 25, MaxDistance = 360,
                FovDeg = 50, TiltUpDeg = 6, Yaw = FlightCameraYaw.Aim, AimDeadZoneDeg = 55, AimTurnGain = 2.5f, AimMaxTurnDegPerSecond = 120
            },
            new FlightCameraPreset
            {
                Name = "orbit", Description = "mouse look: the mouse orbits, the reticle aims, up looks farther",
                DefaultPitchDeg = 14, MinPitchDeg = 3, MaxPitchDeg = 70, DefaultDistance = 80, MinDistance = 25, MaxDistance = 320,
                FovDeg = 50, TiltUpDeg = 9, Yaw = FlightCameraYaw.Free, MouseLook = true, ReticleHeight = .56f
            },
            // CAM-013 (ADR-0123, owner: "Chase camera, flight-sim style"): experimental 3D flight with face-the-mouse on. Mouse look
            // behind the aim, below the ship too, so the view and the nose can point up; the centre reticle aims.
            new FlightCameraPreset
            {
                Name = FreeAim.PresetName, Description = "3D free aim: the mouse turns the view, the ship's nose follows it",
                DefaultPitchDeg = 8, MinPitchDeg = -60, MaxPitchDeg = 70, DefaultDistance = 70, MinDistance = 25, MaxDistance = 320,
                FovDeg = 55, TiltUpDeg = 0, Yaw = FlightCameraYaw.Free, MouseLook = true, ReticleHeight = .5f
            },
        };

        [Game.Core.StaticStateOk("feel prototype: the active flight camera preset, main thread")]
        static int active;
        /// <summary>Bumped on every switch, so the rig re-applies the preset's defaults once.</summary>
        [Game.Core.StaticStateOk("feel prototype: preset change counter, main thread")]
        public static int Version;
        public static FlightCameraPreset Current => All[active];
        public static int ActiveIndex => active;

        public static bool TrySet(string name)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Name == name) { active = i; Version++; return true; }
            return false;
        }

        /// <summary>Owner (2026-10-21): "camera should follow ship rotation by default and C should toggle on/off so you can look around".
        /// C swaps `chase` (follows the heading) and `orbit` (free look: the mouse orbits, the reticle aims); false in the other presets,
        /// where C recentres.</summary>
        public static bool ToggleFreeLook()
        {
            string name = Current.Name;
            return name == "chase" ? TrySet("orbit") : name == "orbit" && TrySet("chase");
        }
    }
}
