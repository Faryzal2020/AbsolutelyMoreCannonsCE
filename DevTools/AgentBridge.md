# AMC In-Game Dev Agent HTTP Bridge (`127.0.0.1:8765`)

`DevAgentBridge` starts a local HTTP JSON server on `http://127.0.0.1:8765/` automatically during RimWorld startup (`[StaticConstructorOnStartup]`).
All requests are safely marshaled to RimWorld's main Unity thread (`Root.Update` or `Root.OnGUI` during `EventType.Repaint` for screenshots).

## Endpoints & PowerShell Examples

### 1. Game Status & Logs
```powershell
# Check current ProgramState (Entry / MapInitializing / Playing), map, ticks, and error counts
Invoke-RestMethod "http://127.0.0.1:8765/status"

# Fetch recent errors/warnings from RimWorld's in-game Log
Invoke-RestMethod "http://127.0.0.1:8765/logs?type=Error&limit=50&stack=true"
Invoke-RestMethod "http://127.0.0.1:8765/logs?search=AMC&limit=50"

# Clear logs
Invoke-RestMethod "http://127.0.0.1:8765/logs/clear"
```

### 2. Start Quicktest Map & God Mode
```powershell
# From the Main Menu (ProgramState.Entry), generate and load a dev quicktest map
Invoke-RestMethod "http://127.0.0.1:8765/quicktest"

# Enable God Mode
Invoke-RestMethod "http://127.0.0.1:8765/godmode?enable=true"
```

### 3. Spawning Buildings, Materials, and Pawns
```powershell
# Spawn the Automated Ammunition Fabricator (automatically clears footprint and connects power)
Invoke-RestMethod "http://127.0.0.1:8765/spawn?def=AMC_AmmoFabricator&power=true"

# Spawn raw materials near the fabricator
Invoke-RestMethod "http://127.0.0.1:8765/spawn?def=Steel&count=500&near=capacitor"
Invoke-RestMethod "http://127.0.0.1:8765/spawn?def=Plasteel&count=200&near=capacitor"
Invoke-RestMethod "http://127.0.0.1:8765/spawn?def=ComponentIndustrial&count=100&near=capacitor"
Invoke-RestMethod "http://127.0.0.1:8765/spawn?def=FSX&count=200&near=capacitor"
Invoke-RestMethod "http://127.0.0.1:8765/spawn?def=Chemfuel&count=300&near=capacitor"
```

### 4. Selecting the Fabricator & Opening `ITab_AmmoCapacitor`
```powershell
# Select the AMC_AmmoFabricator, jump camera to it, and open the "Fab. Ammo" ITab
Invoke-RestMethod "http://127.0.0.1:8765/select?def=AMC_AmmoFabricator&opentab=true"
```

### 5. Inspecting & Controlling `CompAmmoCapacitor`
```powershell
# Get full capacitor state (power, allowSubstitution, uiStepSize, materials stored/target, active tickets)
Invoke-RestMethod "http://127.0.0.1:8765/capacitor"

# Search available CE ammo recipes
Invoke-RestMethod "http://127.0.0.1:8765/capacitor/recipes?search=556x45&limit=10"

# Set capacitor target for a material (if target < stored, excess automatically drops at output cell)
Invoke-RestMethod "http://127.0.0.1:8765/capacitor/target?def=Steel&target=300"
Invoke-RestMethod "http://127.0.0.1:8765/capacitor/target?def=FSX&target=75"
Invoke-RestMethod "http://127.0.0.1:8765/capacitor/target?def=Chemfuel&target=150"

# Directly deposit material into capacitor (for instant crafting/substitution tests)
Invoke-RestMethod "http://127.0.0.1:8765/capacitor/deposit?def=Steel&count=300"

# Toggle material substitution
Invoke-RestMethod "http://127.0.0.1:8765/capacitor/substitute?enable=true"

# Set UI step size
Invoke-RestMethod "http://127.0.0.1:8765/capacitor/stepsize?val=75"

# Add or update a crafting ticket (One-Time or Continuous)
Invoke-RestMethod "http://127.0.0.1:8765/capacitor/ticket?recipe=MakeAmmo_556x45mmNATO_FMJ&continuous=false&count=3"
Invoke-RestMethod "http://127.0.0.1:8765/capacitor/ticket?recipe=MakeAmmo_556x45mmNATO_FMJ&continuous=true&min=100&max=500"
```

### 6. Stepping Ticks, Time Speed & Verifying Hauling Jobs
```powershell
# Check player colonists and their current jobs
Invoke-RestMethod "http://127.0.0.1:8765/pawns?enablehaul=true"

# Test and start WorkGiver_HaulToAmmoCapacitor on an available colonist
Invoke-RestMethod "http://127.0.0.1:8765/pawns/forcehaul?start=true"

# Step N ticks synchronously on the main thread
Invoke-RestMethod "http://127.0.0.1:8765/tick?count=250"

# Change game speed (0=Paused, 1=Normal, 2=Fast, 3=Superfast, 4=Ultrafast)
Invoke-RestMethod "http://127.0.0.1:8765/speed?val=1"

# Inspect spawned stacks / resourceCounter on the map
Invoke-RestMethod "http://127.0.0.1:8765/map/things?def=Ammo_556x45mmNATO_FMJ"
```

### 7. UI Screenshot Capture
```powershell
# Capture full Unity window including IMGUI / ITab_AmmoCapacitor to a PNG file
Invoke-RestMethod "http://127.0.0.1:8765/screenshot?path=C:\Temp\amc_fabricator_ui.png"
```
